using BCrypt.Net; // Để băm mật khẩu
using SmartTable.Filters; // <-- Thêm filter
using SmartTable.Helpers; // Để dùng EmailHelper
using SmartTable.Models; // <-- Thêm Model
using System.Linq; // <-- Thêm Linq
using System.Text; // Để tạo nội dung email
using System.Web.Mvc;
using System;
using System.Configuration; // <-- Thêm: Cần cho ConfigurationManager
using System.Net.Mail; // <-- Thêm: Cần cho MailAddress
using System.Security.Cryptography;
using System.Data.Entity;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin]
    public class DashboardController : Controller
    {
        private Entities db = new Entities();

        // [GET] /Admin/Dashboard/Index
        public ActionResult Index()
        {
            return View();
        }

        // [GET] /Admin/Dashboard/PartnerLeads
        public ActionResult PartnerLeads()
        {
            var leads = db.PartnerLeads.Where(l => l.Status == "Mới").ToList();
            return View(leads);
        }

        // Thay thế GenerateRandomPassword bằng hàm an toàn
        private string GenerateSecureRandomPassword(int length = 10)
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[data[i] % chars.Length];
            }
            return new string(result);
        }
        [HttpGet] // <-- Action này chỉ để router tìm thấy đường dẫn
        public ActionResult RejectPartner(int? leadId)
        {
            // Lệnh này ngăn người dùng truy cập trực tiếp bằng URL (bằng phương thức GET)
            return HttpNotFound();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- LOGIC DUYỆT ĐỐI TÁC ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApprovePartner(int leadId)
        {
            var lead = db.PartnerLeads.Find(leadId);
            if (lead == null) return HttpNotFound();

            using (var tran = db.Database.BeginTransaction())
            {
                try
                {
                    var existingUser = db.Users.FirstOrDefault(u => u.email == lead.Email);
                    Users partnerUser;
                    string randomPassword = GenerateSecureRandomPassword();

                    // 1. Xử lý User (Tạo mới hoặc Nâng cấp vai trò)
                    if (existingUser != null)
                    {
                        existingUser.role = "business";
                        partnerUser = existingUser;
                    }
                    else
                    {
                        partnerUser = new Users
                        {
                            email = lead.Email,
                            full_name = lead.ContactName,
                            phone = lead.ContactPhone,
                            role = "business",
                            password_hash = BCrypt.Net.BCrypt.HashPassword(randomPassword),
                            created_at = DateTime.Now
                        };
                        db.Users.Add(partnerUser);
                    }

                    // 2. LƯU THAY ĐỔI LẦN 1 (BẮT BUỘC để có partnerUser.user_id)
                    db.SaveChanges();

                    // 3. TẠO VÀ LIÊN KẾT NHÀ HÀNG MỚI (CHỈ CẦN CODE NÀY)
                    var newRestaurant = new Restaurants
                    {
                        user_id = partnerUser.user_id,
                        name = lead.RestaurantName,
                        address = lead.Address,
                        max_tables = lead.TotalSeats,
                        opening_hours = lead.OpeningTime + "-" + lead.ClosingTime,
                        is_approved = true,
                        Image = lead.PhotoLink ?? "https://via.placeholder.com/400x300.png?text=SmartTable",
                        created_at = DateTime.Now,

                        // === CHUYỂN DỮ LIỆU CHI TIẾT TỪ PARTNERLEADS ===
                        CuisineStyle = lead.CuisineStyle,
                        ServiceDescription = lead.ServiceDescription,
                        ServiceTypes = lead.ServiceTypes, // Kiểu phục vụ (Gọi món, Buffet)
                        AverageBill = lead.AverageBill,
                        FloorCount = lead.FloorCount,
                        BusyHours = lead.BusyHours,
                        SlowHours = lead.SlowHours,
                        SignatureDishes = lead.SignatureDishes,
                        PartnershipGoal = lead.PartnershipGoal,
                        ServicePackage = lead.ServicePackage,
                        ContactName = lead.ContactName,
                        ContactPhone = lead.ContactPhone,
                        ContactRole = lead.ContactRole,
                        Website = lead.Website,
                        SpaceDescription = lead.SpaceDescription,
                        Amenities = lead.Amenities
                        // ... (Không cần các trường Other và Previous Partnership)
                    };
                    db.Restaurants.Add(newRestaurant);

                    // 4. GỬI EMAIL CHÀO MỪNG (Chỉ gửi nếu là user mới)
                    if (existingUser == null)
                    {
                        string subject = "Chào mừng Đối tác! Tài khoản Smart-Table của bạn đã được duyệt.";
                        string body = $"Chào {partnerUser.full_name},\n\n" +
                                      $"Tài khoản đối tác của bạn đã được duyệt. Bạn có thể đăng nhập bằng thông tin sau:\n" +
                                      $"Email: {partnerUser.email}\n" +
                                      $"Mật khẩu: {randomPassword}\n\n" +
                                      $"Vui lòng đổi mật khẩu sau khi đăng nhập lần đầu.\nTrân trọng,\nĐội ngũ Smart-Table.";

                        EmailHelper.SendEmail(partnerUser.email, subject, body);
                    }

                    // cuối cùng cập nhật lead.Status và lưu
                    lead.Status = "Đã duyệt";
                    db.SaveChanges(); // Lưu nốt Nhà hàng và trạng thái Lead

                    tran.Commit();
                    TempData["SuccessMessage"] = "Đã duyệt đối tác.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    // log ex
                    TempData["ErrorMessage"] = "Duyệt thất bại.";
                    return RedirectToAction("PartnerLeads");
                }
            }
        }
        // (Trong file Areas/Admin/Controllers/DashboardController.cs)

        [AuthorizeAdmin]
        public ActionResult PartnerLeadDetails(int id)
        {
            var lead = db.PartnerLeads.Find(id);
            if (lead == null)
            {
                return HttpNotFound();
            }
            return View(lead); // Gửi Model PartnerLeads chi tiết sang View
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectPartner(int leadId)
        {
            var lead = db.PartnerLeads.Find(leadId);
            if (lead == null)
            {
                return HttpNotFound();
            }

            try
            {
                // 1. GỬI EMAIL THÔNG BÁO TỪ CHỐI
                string subject = "Thông báo về đơn đăng ký đối tác Smart-Table của bạn";
                string body = $"Kính gửi {lead.ContactName},\n\n" +
                              $"Cảm ơn bạn đã quan tâm và gửi đơn đăng ký đối tác nhà hàng Smart-Table cho nhà hàng **{lead.RestaurantName}**.\n\n" +
                              $"Sau khi xem xét, chúng tôi rất tiếc phải thông báo rằng đơn đăng ký của bạn **chưa thể được phê duyệt** vào thời điểm này.\n\n" +
                              // Lý do giả định (bạn có thể tùy chỉnh)
                              $"Lý do chính có thể bao gồm: Thông tin chưa đầy đủ, hoặc khu vực của bạn đã có đủ đối tác trong mạng lưới hiện tại của chúng tôi.\n\n" +
                              $"Bạn có thể liên hệ với chúng tôi để biết thêm chi tiết hoặc nộp lại đơn đăng ký sau 03 tháng.\n\n" +
                              $"Trân trọng,\nĐội ngũ Smart-Table.";

                // Sử dụng EmailHelper để gửi email
                SmartTable.Helpers.EmailHelper.SendEmail(lead.Email, subject, body);

                // 2. Cập nhật trạng thái và lưu lần cuối
                lead.Status = "Đã từ chối";
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã từ chối thành công đối tác {lead.RestaurantName} và gửi email thông báo.";
                return RedirectToAction("PartnerLeads");
            }
            catch (Exception ex)
            {
                // Nếu lỗi xảy ra (thường là lỗi gửi email hoặc lỗi DB)
                TempData["ErrorMessage"] = "Lỗi khi từ chối đơn đăng ký. Vui lòng kiểm tra cấu hình Email hoặc Database. Lỗi: " + ex.Message;
                return RedirectToAction("PartnerLeads");
            }
        }
    } 
} 