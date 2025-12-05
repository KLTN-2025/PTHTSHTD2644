using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using SmartTable.Models;
using System.Data.Entity;
using System.Diagnostics;

namespace SmartTable.Controllers
{
    public class ChatController : Controller
    {
        private Entities db = new Entities();

        // Bộ đệm để tránh spam API (Throttle)
        private static readonly ConcurrentDictionary<string, DateTime> _lastCall = new ConcurrentDictionary<string, DateTime>();
        private const int MinSecondsBetweenCalls = 3;

        // Cache dữ liệu nhà hàng để dùng khi API lỗi (Fallback) hoặc để tối ưu truy vấn
        private static string _cachedRestaurantsSummary = null;
        private static DateTime _cachedRestaurantsAt = DateTime.MinValue;
        private static readonly TimeSpan RestaurantsCacheDuration = TimeSpan.FromMinutes(10);

        [HttpPost]
        public async Task<ActionResult> SendMessage()
        {
            // 1. Đọc nội dung tin nhắn
            Request.InputStream.Position = 0;
            string body;
            using (var sr = new StreamReader(Request.InputStream))
            {
                body = await sr.ReadToEndAsync();
            }

            string userMessage = null;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                if (obj != null && obj.ContainsKey("message"))
                {
                    userMessage = (obj["message"] ?? string.Empty).ToString().Trim();
                }
            }
            catch { /* Ignore parse error */ }

            if (string.IsNullOrWhiteSpace(userMessage))
                return Json(new { success = false, reply = "Vui lòng nhập câu hỏi." });

            // 2. Kiểm tra tần suất gửi (Throttle)
            var clientKey = Request.UserHostAddress ?? "anon";
            if (_lastCall.TryGetValue(clientKey, out var last) && (DateTime.UtcNow - last).TotalSeconds < MinSecondsBetweenCalls)
            {
                return Json(new { success = false, reply = "Bạn gửi quá nhanh, vui lòng đợi một chút." });
            }
            _lastCall[clientKey] = DateTime.UtcNow;

            // 3. TÌM KIẾM THÔNG MINH & LẤY DỮ LIỆU (CONTEXT)
            

            List<object> restaurantsContext = new List<object>();
            try
            {
                // Từ khóa tìm kiếm sơ bộ (đơn giản)
                string keyword = userMessage.ToLower();

                var query = db.Restaurants
                    .Include(r => r.MenuItems)
                    .Where(r => r.is_approved == true);

            
                var matchedRestaurants = new List<Restaurants>();

                if (keyword.Length > 3)
                {
                    matchedRestaurants = query.Where(r =>
                        r.name.ToLower().Contains(keyword) ||
                        r.address.ToLower().Contains(keyword) ||
                        r.CuisineStyle.ToLower().Contains(keyword) ||
                        r.MenuItems.Any(m => m.name.ToLower().Contains(keyword))
                    ).Take(5).ToList();
                }

                if (matchedRestaurants.Count == 0)
                {
                    matchedRestaurants = query.OrderByDescending(r => r.restaurant_id).Take(10).ToList();
                }

                // --- CHUẨN BỊ DỮ LIỆU ĐẦY ĐỦ CHO AI ---
                restaurantsContext = matchedRestaurants.Select(r => new
                {
                    Ten = r.name,
                    DiaChi = r.address,
                    GioMoCua = r.opening_hours,
                    SDT = r.ContactPhone ?? "Không có",
                    MoTa = r.description ?? "Không có mô tả",
                    AnhDaiDien = r.Image, 

                    // Chi tiết dịch vụ
                    PhongCach = r.CuisineStyle,
                    LoaiHinh = r.ServiceDescription,
                    GiaTB = r.AverageBill,
                    TienIch = r.Amenities, 

                    // Thực đơn ĐẦY ĐỦ (Lấy tên và giá)
                    ThucDon = r.MenuItems.Select(m => $"{m.name} ({m.price:N0}đ)").ToList()
                }).ToList<object>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi đọc DB: " + ex.Message);
            }

            // 4. Tạo System Prompt
            var contextJson = JsonConvert.SerializeObject(restaurantsContext);

            var systemPrompt = $@"Bạn là trợ lý ảo thông minh của SmartTable.
            
            DỮ LIỆU NHÀ HÀNG HIỆN CÓ (JSON):
            {contextJson}

            NHIỆM VỤ:
            1. Trả lời câu hỏi của khách hàng dựa CHÍNH XÁC vào dữ liệu trên.
            2. Nếu khách hỏi 'ảnh' hoặc 'hình', hãy trả về link ảnh dưới dạng Markdown: ![Tên quán](LinkAnh).
            3. Nếu khách hỏi 'thực đơn' hoặc 'giá', hãy liệt kê các món trong danh sách 'ThucDon'.
            4. Nếu khách hỏi tiện ích (wifi, máy lạnh...), hãy xem trường 'TienIch'.
            5. Nếu khách hỏi thông tin không có trong JSON, hãy nói 'Xin lỗi, SmartTable chưa cập nhật thông tin này'.
            6. Trả lời ngắn gọn, thân thiện, dùng tiếng Việt.

            Ví dụ trả lời ảnh:
            'Dạ, đây là hình ảnh của quán ạ: ![Hảo Hán Food](https://...)'
            ";

            var modelName = ConfigurationManager.AppSettings["DeepSeekModel"] ?? "deepseek/deepseek-r1:free";

            var requestData = new DeepSeekRequest
            {
                Model = modelName,
                Messages = new List<DeepSeekMessage>
                {
                    new DeepSeekMessage { Role = "system", Content = systemPrompt },
                    new DeepSeekMessage { Role = "user", Content = userMessage }
                },
                Temperature = 0.7,
                MaxTokens = 1500 
            };

            // 5. Gọi API thông qua Helper
            var deepResult = await DeepSeekHelper.GetResponseAsync(requestData);

            // 6. Xử lý kết quả & Fallback (Dự phòng khi quá tải)
            if (!deepResult.Success)
            {
                if ((deepResult.Status != null && deepResult.Status.Contains("429")) ||
                    (deepResult.Reply != null && deepResult.Reply.Contains("rate limit")))
                {
                    return Json(new
                    {
                        success = false,
                        reply = "AI đang bận (quá tải). Vui lòng thử lại sau giây lát."
                    });
                }

                return Json(new { success = false, reply = "Lỗi AI: " + deepResult.Reply });
            }

            return Json(new { success = true, reply = deepResult.Reply });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}