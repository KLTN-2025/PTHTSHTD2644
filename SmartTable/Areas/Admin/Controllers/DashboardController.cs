using BCrypt.Net; // Để băm mật khẩu
using SmartTable.Filters; // <-- Thêm filter
using SmartTable.Helpers; // Để dùng EmailHelper
using SmartTable.Models; // <-- Thêm Model
using System.Linq; // <-- Thêm Linq
using System.Text; // Để tạo nội dung email
using System.Web.Mvc;
using System;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] // <-- BẮT BUỘC: Khóa toàn bộ Controller này
    public class DashboardController : Controller
    {
        private Entities db = new Entities();

        // [GET] /Admin/Dashboard/Index
        public ActionResult Index()
        {
            // Trang chào mừng Admin
            return View();
        }

        // [GET] /Admin/Dashboard/PartnerLeads
        public ActionResult PartnerLeads()
        {
            // Lấy danh sách đối tác đăng ký (chưa duyệt)
            var leads = db.PartnerLeads.Where(l => l.Status == "Mới").ToList();
            return View(leads);
        }
        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // (Thêm vào bên trong class DashboardController)

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApprovePartner(int leadId) // Nhận ID từ nút bấm
        {
            // 1. Tìm đơn đăng ký (Lead)
            var lead = db.PartnerLeads.Find(leadId);
            if (lead == null)
            {
                return HttpNotFound();
            }

            // 2. Kiểm tra xem email này đã tồn tại trong bảng Users chưa
            var existingUser = db.Users.FirstOrDefault(u => u.email == lead.Email);

            Users partnerUser;

            if (existingUser != null)
            {
                // Nếu User đã tồn tại (ví dụ: họ đăng ký làm khách trước đó)
                // Chỉ cần nâng cấp vai trò của họ
                existingUser.role = "business";
                partnerUser = existingUser;
            }
            else
            {
                // Nếu User chưa tồn tại, tạo User mới
                string randomPassword = GenerateRandomPassword(); // Tạo mật khẩu ngẫu nhiên

                partnerUser = new Users
                {
                    email = lead.Email,
                    full_name = lead.ContactName,
                    phone = lead.ContactPhone,
                    role = "business", // Đặt vai trò là "business"
                    password_hash = BCrypt.Net.BCrypt.HashPassword(randomPassword), // Băm mật khẩu
                    created_at = DateTime.Now
                };
                db.Users.Add(partnerUser);

                // Gửi email chào mừng chứa mật khẩu
                try
                {
                    string subject = "Chào mừng Đối tác! Tài khoản Smart-Table của bạn đã được duyệt.";
                    string body = $"Chào {partnerUser.full_name},\n\n" +
                                  $"Tài khoản đối tác của bạn đã được duyệt. Bạn có thể đăng nhập bằng thông tin sau:\n" +
                                  $"Email: {partnerUser.email}\n" +
                                  $"Mật khẩu: {randomPassword}\n\n" +
                                  $"Vui lòng đổi mật khẩu sau khi đăng nhập lần đầu.\nTrân trọng,\nĐội ngũ Smart-Table.";

                    EmailHelper.SendEmail(partnerUser.email, subject, body);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("LỖI GỬI MAIL DUYỆT: " + ex.Message);
                    // Có thể thêm TempData["ErrorMessage"] nếu muốn
                }
            }

            // 3. Cập nhật trạng thái đơn đăng ký
            lead.Status = "Đã duyệt";

            // 4. (Quan trọng) Liên kết User mới với Nhà hàng (nếu bạn có cột user_id trong bảng Restaurants)
            // var restaurant = db.Restaurants.FirstOrDefault(r => r.email == lead.Email); // Hoặc tìm theo tên
            // if(restaurant != null)
            // {
            //     restaurant.user_id = partnerUser.user_id; // Gán ID user mới tạo
            // }

            db.SaveChanges(); // Lưu tất cả thay đổi (cả User mới và Lead)

            TempData["SuccessMessage"] = "Đã duyệt thành công đối tác: " + lead.RestaurantName;
            return RedirectToAction("PartnerLeads");
        }

    }
}