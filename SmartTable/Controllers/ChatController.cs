using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using SmartTable.Models;

namespace SmartTable.Controllers
{
    public class ChatController : Controller
    {
        private readonly Entities db = new Entities();

        // chống spam
        private static readonly ConcurrentDictionary<string, DateTime> _lastCall =
            new ConcurrentDictionary<string, DateTime>();
        private const int MinSecondsBetweenCalls = 2;

        // trí nhớ ngắn hạn theo IP
        private class ChatSessionState
        {
            public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
            public int? LastRestaurantId { get; set; }
            public string LastRestaurantName { get; set; }
            public string LastFromPlace { get; set; }
        }

        private static readonly ConcurrentDictionary<string, ChatSessionState> _sessions =
            new ConcurrentDictionary<string, ChatSessionState>();
        private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

        [HttpPost]
        public async Task<ActionResult> SendMessage()
        {
            var userMessage = await ReadMessageAsync();
            if (string.IsNullOrWhiteSpace(userMessage))
                return Json(new { success = false, reply = "Bạn muốn hỏi gì thêm không?" });

            // throttle
            var clientKey = GetClientKey();
            if (_lastCall.TryGetValue(clientKey, out var last) &&
                (DateTime.UtcNow - last).TotalSeconds < MinSecondsBetweenCalls)
            {
                return Json(new { success = false, reply = "Bạn gửi hơi nhanh, chờ 1–2 giây rồi gửi lại nhé." });
            }
            _lastCall[clientKey] = DateTime.UtcNow;

            // session
            var session = GetSession(clientKey);

            // phân tích entity (không tự trả lời)
            var parsed = AnalyzeQuestion(userMessage, session);

            // candidates nhà hàng
            var candidates = await GetRestaurantCandidatesAsync(parsed.RestaurantCandidate, take: 12);

            // focus (chỉ để đổ menu/ảnh/review đúng quán)
            var focus = PickBestRestaurant(candidates, parsed.RestaurantCandidate, userMessage);
            if (focus != null)
            {
                session.LastRestaurantId = focus.restaurant_id;
                session.LastRestaurantName = focus.name;
            }

            var focusIds = new List<int>();
            if (focus != null) focusIds.Add(focus.restaurant_id);

            if (focusIds.Count == 0 && candidates.Count > 0)
                focusIds.AddRange(candidates.Take(3).Select(r => r.restaurant_id));

            var context = await BuildContextAsync(candidates, focusIds);

            var systemPrompt = BuildDeepSeekSystemPrompt();

            var payload = new
            {
                Question = userMessage,
                Entity = new
                {
                    FromPlace = parsed.FromPlace ?? session.LastFromPlace ?? "",
                    ToPlace = parsed.ToPlace ?? "",
                    RestaurantCandidate = parsed.RestaurantCandidate ?? "",
                    LastRestaurantName = session.LastRestaurantName ?? "",
                    LastRestaurantId = session.LastRestaurantId
                },
                Data = context
            };

            var requestData = new DeepSeekRequest
            {
                Model = ConfigurationManager.AppSettings["DeepSeekModel"] ?? "deepseek/deepseek-r1:free",
                Messages = new List<DeepSeekMessage>
                {
                    new DeepSeekMessage { Role = "system", Content = systemPrompt },
                    new DeepSeekMessage { Role = "user", Content = JsonConvert.SerializeObject(payload) }
                },
                Temperature = 0.5,
                MaxTokens = 900
            };

            DeepSeekResult deepResult;
            try
            {
                deepResult = await DeepSeekHelper.GetResponseAsync(requestData);
            }
            catch
            {
                return Json(new { success = false, reply = "AI đang gặp sự cố kết nối. Bạn thử lại sau ít giây nhé." });
            }

            if (deepResult == null || !deepResult.Success || string.IsNullOrWhiteSpace(deepResult.Reply))
            {
                return Json(new { success = true, reply = "Mình đang xử lý hơi chậm. Bạn thử hỏi lại ngắn gọn hơn giúp mình nhé." });
            }

            if (!string.IsNullOrWhiteSpace(parsed.FromPlace))
                session.LastFromPlace = parsed.FromPlace;

            return Json(new { success = true, reply = deepResult.Reply });
        }

        private string BuildDeepSeekSystemPrompt()
        {
            return @"
Bạn là trợ lý SmartTable – tư vấn nhà hàng hiểu chuyện.

MỤC TIÊU:
- Hiểu điều khách MUỐN BIẾT, không chỉ điều khách NÓI.
- Ưu tiên trải nghiệm người dùng hơn sự đúng tuyệt đối.
- Không lan man.

CHUỖI SUY LUẬN BẮT BUỘC:
1) Phân tích câu hỏi theo ngữ nghĩa
2) Xác định thực thể: nhà hàng, điểm đi, điểm đến
3) Xác định ý định: thông tin quán / menu giá / ảnh / đánh giá / chỉ đường / bao xa bao lâu
4) Nếu đủ dữ liệu -> trả lời ngay (không hỏi lại)
5) Chỉ hỏi lại khi KHÔNG thể suy ra hợp lý

NGUYÊN TẮC QUAN TRỌNG:
- Nếu câu hỏi có nhắc tên nhà hàng (dù không đầy đủ), coi đó là nhà hàng họ quan tâm.
- Tuyệt đối không trả lời kiểu 'SmartTable chưa cập nhật...' cho các câu hỏi về: xa không, bao lâu, chỉ đường.
- Nếu dữ liệu không có khoảng cách chính xác: ĐƯỢC PHÉP ước lượng hợp lý và dùng ngôn ngữ mờ có kiểm soát:
  + chắc: 'khoảng', 'tầm'
  + vừa: 'ước chừng', 'thường'
  + thấp: 'nếu đi từ khu vực trung tâm...'

CẤU TRÚC TRẢ LỜI:
- 1 câu kết luận nhanh (gần/xa, tiện/khó)
- 1–2 câu số ước lượng (km/phút) phù hợp ngữ cảnh
- 1 câu nhận xét giúp khách quyết định
- thêm 1 gợi ý nhỏ (không hỏi ngược)

CHỈ ĐƯỜNG / MUỐN TỚI:
- Nếu khách nói 'tôi muốn tới', 'chỉ đường', 'map': BẮT BUỘC trả ĐỊA CHỈ + link Map trong dữ liệu (Google Maps).

ẢNH:
- Nếu có ImagesByRestaurant: trả 1–3 ảnh Markdown: ![Tên](Url)
- Nếu không có thì dùng AnhDaiDien
- Nếu không có ảnh -> nói quán chưa cập nhật ảnh.

MENU:
- Liệt kê 8–12 món tiêu biểu + giá. Nếu menu trống -> nói quán chưa cập nhật thực đơn.

ĐÁNH GIÁ:
- Nếu quán chưa có đánh giá -> nói rõ 'hiện chưa có đánh giá trên SmartTable', không được bảo không tìm thấy quán.

Không bịa dữ liệu cụ thể như địa chỉ/số điện thoại/menu nếu không có trong JSON.
";
        }

        // ========= ANALYZE  =========
        private class ParsedQuestion
        {
            public string FromPlace { get; set; }
            public string ToPlace { get; set; }
            public string RestaurantCandidate { get; set; }
        }

        private ParsedQuestion AnalyzeQuestion(string msg, ChatSessionState session)
        {
            var raw = (msg ?? "").Trim();
            var s = raw.ToLowerInvariant();

            var pq = new ParsedQuestion();

            int idxTu = s.IndexOf("từ ", StringComparison.OrdinalIgnoreCase);
            int idxDen = s.IndexOf(" đến ", StringComparison.OrdinalIgnoreCase);
            if (idxTu >= 0 && idxDen > idxTu)
            {
                pq.FromPlace = raw.Substring(idxTu + 2).Substring(0, idxDen - (idxTu + 2)).Trim();
                pq.ToPlace = raw.Substring(idxDen + 4).Trim();
            }
            else
            {
                pq.ToPlace = ExtractDestinationPhrase(raw);
            }

            pq.RestaurantCandidate = (pq.ToPlace ?? "").Trim();

            if (LooksLikeFollowUp(s) && session?.LastRestaurantId != null && !string.IsNullOrWhiteSpace(session.LastRestaurantName))
                pq.RestaurantCandidate = session.LastRestaurantName;

            return pq;
        }

        private bool LooksLikeFollowUp(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            return s.Contains("quán đó") || s.Contains("quán này") || s.Contains("ở đó") || s.Contains("ở đây") ||
                   s.Contains("chỗ đó") || s.Contains("chỗ này") || s.Contains("món gì") || s.Contains("thực đơn") ||
                   s.Contains("giá sao") || s.Contains("đánh giá") || s.Contains("hình") || s.Contains("ảnh");
        }

        private string ExtractDestinationPhrase(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return "";
            var s = msg.Trim().ToLowerInvariant();

            string[] marks = { "đến nhà hàng ", "tới nhà hàng ", "đến quán ", "tới quán ", "đến ", "tới ", "ghé ", "qua " };
            foreach (var m in marks)
            {
                var idx = s.IndexOf(m, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var tail = msg.Substring(idx + m.Length).Trim();

                    string[] cut = { "bao xa", "xa không", "mất bao lâu", "bao lâu", "đi bao lâu", "chỉ đường", "đường đi", "map", "bản đồ" };
                    foreach (var c in cut)
                    {
                        var k = tail.ToLowerInvariant().IndexOf(c, StringComparison.OrdinalIgnoreCase);
                        if (k > 0) tail = tail.Substring(0, k).Trim();
                    }

                    return tail.Trim().Trim('.', '?', '!', ',', ';', ':');
                }
            }
            return "";
        }

        // ========= DB: candidates =========
        private async Task<List<Restaurants>> GetRestaurantCandidatesAsync(string candidate, int take)
        {
            candidate = (candidate ?? "").Trim().ToLowerInvariant();

            var q = db.Restaurants.Where(r => r.is_approved == true);

            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length >= 2)
            {
                q = q.Where(r =>
                    (r.name ?? "").ToLower().Contains(candidate) ||
                    (r.address ?? "").ToLower().Contains(candidate) ||
                    (r.CuisineStyle ?? "").ToLower().Contains(candidate) ||
                    (r.City ?? "").ToLower().Contains(candidate) ||
                    (r.Area ?? "").ToLower().Contains(candidate) ||
                    (r.SignatureDishes ?? "").ToLower().Contains(candidate)
                );
            }

            var list = await q.OrderByDescending(r => r.restaurant_id).Take(take).ToListAsync();

            if (list.Count == 0)
            {
                list = await db.Restaurants
                    .Where(r => r.is_approved == true)
                    .OrderByDescending(r => r.restaurant_id)
                    .Take(take)
                    .ToListAsync();
            }

            return list;
        }

        private Restaurants PickBestRestaurant(List<Restaurants> candidates, string candidateText, string fullQuestion)
        {
            if (candidates == null || candidates.Count == 0) return null;

            var ct = (candidateText ?? "").Trim().ToLowerInvariant();
            var qt = (fullQuestion ?? "").Trim().ToLowerInvariant();

            int Score(Restaurants r)
            {
                int sc = 0;
                var name = (r.name ?? "").ToLowerInvariant();
                var addr = (r.address ?? "").ToLowerInvariant();
                var cuisine = (r.CuisineStyle ?? "").ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(ct))
                {
                    if (name.Contains(ct)) sc += 100;
                    foreach (var t in Tokenize(ct))
                        if (name.Contains(t)) sc += 25;
                }

                foreach (var t in Tokenize(qt))
                {
                    if (name.Contains(t)) sc += 20;
                    if (addr.Contains(t)) sc += 10;
                    if (cuisine.Contains(t)) sc += 6;
                }

                if (!string.IsNullOrWhiteSpace(r.address)) sc += 2;
                if (!string.IsNullOrWhiteSpace(r.Image)) sc += 2;

                return sc;
            }

            return candidates
                .Select(r => new { R = r, S = Score(r) })
                .OrderByDescending(x => x.S)
                .ThenByDescending(x => x.R.restaurant_id)
                .First().R;
        }

        private List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            var s = text.ToLowerInvariant();

            var chars = s.Select(ch => char.IsLetterOrDigit(ch) || ch == ' ' ? ch : ' ').ToArray();
            s = new string(chars);

            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "từ","đến","tới","qua","ghé","nhà","hàng","quán","ở","đâu","bao","xa","xa","không","mất","bao","lâu",
                "đi","chỉ","đường","map","bản","đồ","cho","mình","tôi","em","anh","chị","nhé","ạ"
            };

            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 2 && !stop.Contains(t))
                    .Distinct()
                    .Take(10)
                    .ToList();
        }

        // ========= Build context: Restaurants + Menu + Images + Reviews =========
        private async Task<object> BuildContextAsync(List<Restaurants> candidates, List<int> focusIds)
        {
            focusIds = (focusIds ?? new List<int>()).Distinct().ToList();

            var restaurantsBlock = candidates.Select(r => new
            {
                RestaurantId = r.restaurant_id,
                Ten = r.name ?? "Chưa có tên",
                DiaChi = r.address ?? "Chưa cập nhật",
                KhuVuc = $"{(r.Area ?? "").Trim()} {(string.IsNullOrWhiteSpace(r.City) ? "" : "- " + r.City)}".Trim(),
                GioMoCua = r.opening_hours ?? "Chưa cập nhật",
                SDT = r.ContactPhone ?? "",
                Website = r.Website ?? "",
                PhongCach = r.CuisineStyle ?? "",
                GiaTB = r.AverageBill ?? "",
                TienIch = r.Amenities ?? "",
                AnhDaiDien = NormalizeUrl(r.Image),
                Map = BuildGoogleMapsLink(r.name, r.address, r.latitude, r.longitude)
            }).ToList();

            // ===== KEY FIX: restaurant_id có thể int hoặc int? -> ép về int? rồi coalesce =====

            // Menu
            var menus = await db.MenuItems
                .Where(m => m.is_available == true && focusIds.Contains(((int?)m.restaurant_id ?? -1)))
                .OrderBy(m => m.restaurant_id)
                .ThenBy(m => m.category)
                .ThenBy(m => m.name)
                .Take(400)
                .ToListAsync();

            var menuByRestaurant = menus
                .Select(m => new
                {
                    Rid = (int?)m.restaurant_id,
                    TenMon = m.name ?? "",
                    Gia = m.price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ",
                    DanhMuc = m.category ?? "",
                    MoTa = m.description ?? "",
                    AnhMon = NormalizeUrl(m.Image)
                })
                .Where(x => x.Rid.HasValue && x.Rid.Value > 0)
                .GroupBy(x => x.Rid.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new
                    {
                        x.TenMon,
                        x.Gia,
                        x.DanhMuc,
                        x.MoTa,
                        x.AnhMon
                    }).ToList()
                );

            // Images
            var imgs = await db.RestaurantImages
                .Where(x => focusIds.Contains(((int?)x.restaurant_id ?? -1)))
                .OrderByDescending(x => x.image_id)
                .Take(120)
                .ToListAsync();

            var imagesByRestaurant = imgs
                .Select(x => new
                {
                    Rid = (int?)x.restaurant_id,
                    Url = NormalizeUrl(x.image_url),
                    MoTa = x.description ?? ""
                })
                .Where(x => x.Rid.HasValue && x.Rid.Value > 0 && !string.IsNullOrWhiteSpace(x.Url))
                .GroupBy(x => x.Rid.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Take(12).Select(x => new { x.Url, x.MoTa }).ToList()
                );

            // Reviews
            var rvs = await db.Reviews
                .Where(x => focusIds.Contains(((int?)x.restaurant_id ?? -1)))
                .OrderByDescending(x => x.created_at)
                .Take(200)
                .ToListAsync();

            var reviewsByRestaurant = rvs
                .Select(x => new
                {
                    Rid = (int?)x.restaurant_id,
                    Rating = x.rating,
                    Comment = x.comment,
                    CreatedAt = x.created_at
                })
                .Where(x => x.Rid.HasValue && x.Rid.Value > 0)
                .GroupBy(x => x.Rid.Value)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Count = g.Count(),
                        AvgRating = g.Any() ? Math.Round(g.Average(x => (double)x.Rating), 1) : 0.0,
                        Recent = g.Take(5).Select(x => new
                        {
                            Sao = x.Rating,
                            BinhLuan = x.Comment ?? "",
                            Ngay = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("dd/MM/yyyy") : ""
                        }).ToList()
                    }
                );

            return new
            {
                FocusRestaurantIds = focusIds,
                Restaurants = restaurantsBlock,
                MenuByRestaurant = menuByRestaurant,
                ImagesByRestaurant = imagesByRestaurant,
                ReviewsByRestaurant = reviewsByRestaurant
            };
        }

        private string NormalizeUrl(string url)
        {
            url = (url ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url)) return "";

            url = url.Replace("\\", "/");
            if (url.StartsWith("~")) url = url.Substring(1);

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;

            if (!url.StartsWith("/")) url = "/" + url;

            var req = Request?.Url;
            if (req == null) return url;

            var baseUrl = $"{req.Scheme}://{req.Authority}";
            return baseUrl + url;
        }

        private string BuildGoogleMapsLink(string name, string address, double? lat, double? lng)
        {
            if (lat.HasValue && lng.HasValue &&
                Math.Abs(lat.Value) > 0.000001 && Math.Abs(lng.Value) > 0.000001)
            {
                var q = Uri.EscapeDataString(
                    lat.Value.ToString(CultureInfo.InvariantCulture) + "," +
                    lng.Value.ToString(CultureInfo.InvariantCulture)
                );
                return "https://www.google.com/maps/search/?api=1&query=" + q;
            }

            var text = $"{(name ?? "").Trim()} {(address ?? "").Trim()}".Trim();
            if (string.IsNullOrWhiteSpace(text)) return "";
            return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(text);
        }

        private async Task<string> ReadMessageAsync()
        {
            Request.InputStream.Position = 0;
            using (var sr = new StreamReader(Request.InputStream, Encoding.UTF8))
            {
                var body = await sr.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body)) return "";

                try
                {
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                    if (obj != null && obj.ContainsKey("message"))
                        return (obj["message"] ?? "").ToString().Trim();
                }
                catch { }

                return "";
            }
        }

        private string GetClientKey()
        {
            var xff = Request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrWhiteSpace(xff))
                return xff.Split(',')[0].Trim();

            return Request.UserHostAddress ?? "anon";
        }

        private ChatSessionState GetSession(string key)
        {
            foreach (var kv in _sessions.ToArray())
            {
                if ((DateTime.UtcNow - kv.Value.LastSeenUtc) > SessionTtl)
                    _sessions.TryRemove(kv.Key, out _);
            }

            var st = _sessions.GetOrAdd(key, _ => new ChatSessionState());
            st.LastSeenUtc = DateTime.UtcNow;
            return st;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
