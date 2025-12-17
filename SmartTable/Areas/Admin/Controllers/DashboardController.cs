using BCrypt.Net; 
using SmartTable.Filters;
using SmartTable.Helpers; 
using SmartTable.Models;
using System.Linq; 
using System.Text; 
using System.Web.Mvc;
using System;
using System.Configuration; 
using System.Net.Mail; 
using System.Security.Cryptography;
using System.Data.Entity;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin]
    public class DashboardController : Controller
    {
        private Entities db = new Entities();

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult PartnerLeads()
        {
            var leads = db.PartnerLeads.Where(l => l.Status == "Mới").ToList();
            return View(leads);
        }

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
        [HttpGet] 
        public ActionResult RejectPartner(int? leadId)
        {
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

                    db.SaveChanges();

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

                        CuisineStyle = lead.CuisineStyle,
                        ServiceDescription = lead.ServiceDescription,
                        ServiceTypes = lead.ServiceTypes, 
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
                    };
                    db.Restaurants.Add(newRestaurant);

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

                    lead.Status = "Đã duyệt";
                    db.SaveChanges(); 

                    tran.Commit();
                    TempData["SuccessMessage"] = "Đã duyệt đối tác.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    TempData["ErrorMessage"] = "Duyệt thất bại.";
                    return RedirectToAction("PartnerLeads");
                }
            }
        }

        [AuthorizeAdmin]
        public ActionResult PartnerLeadDetails(int id)
        {
            var lead = db.PartnerLeads.Find(id);
            if (lead == null)
            {
                return HttpNotFound();
            }
            return View(lead); 
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
                string subject = "Thông báo về đơn đăng ký đối tác Smart-Table của bạn";
                string body = $"Kính gửi {lead.ContactName},\n\n" +
                              $"Cảm ơn bạn đã quan tâm và gửi đơn đăng ký đối tác nhà hàng Smart-Table cho nhà hàng **{lead.RestaurantName}**.\n\n" +
                              $"Sau khi xem xét, chúng tôi rất tiếc phải thông báo rằng đơn đăng ký của bạn **chưa thể được phê duyệt** vào thời điểm này.\n\n" +
                              $"Lý do chính có thể bao gồm: Thông tin chưa đầy đủ, hoặc khu vực của bạn đã có đủ đối tác trong mạng lưới hiện tại của chúng tôi.\n\n" +
                              $"Bạn có thể liên hệ với chúng tôi để biết thêm chi tiết hoặc nộp lại đơn đăng ký sau 03 tháng.\n\n" +
                              $"Trân trọng,\nĐội ngũ Smart-Table.";

                // Sử dụng EmailHelper để gửi email
                SmartTable.Helpers.EmailHelper.SendEmail(lead.Email, subject, body);

                lead.Status = "Đã từ chối";
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã từ chối thành công đối tác {lead.RestaurantName} và gửi email thông báo.";
                return RedirectToAction("PartnerLeads");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi từ chối đơn đăng ký. Vui lòng kiểm tra cấu hình Email hoặc Database. Lỗi: " + ex.Message;
                return RedirectToAction("PartnerLeads");
            }
        }
    } 
} 