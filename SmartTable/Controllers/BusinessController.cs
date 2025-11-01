using SmartTable.Models.ViewModels; // <-- Thêm
using System;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Helpers;
using System.Text;
using System.Configuration;

namespace SmartTable.Controllers
{
    public class BusinessController : Controller
    {
        private Entities db = new Entities();

        // [GET] /Business/Index (Trang giới thiệu)
        public ActionResult Index()
        {
            return View(); // Chỉ trả về trang giới thiệu (Index.cshtml)
        }

        // [GET] /Business/RegisterPartner (Hiển thị Form)
        [HttpGet]
        public ActionResult RegisterPartner()
        {
            var model = new PartnerRegistrationViewModel();
            return View(model); // Trả về trang form (RegisterPartner.cshtml)
        }

        // [POST] /Business/RegisterPartner (Xử lý Form)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterPartner(PartnerRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.";
                return View(model); // Giữ lại dữ liệu và báo lỗi trên trang form
            }

            try
            {
                // === LƯU VÀO DATABASE ===
                // (Bạn cần tạo bảng 'PartnerLeads' và cập nhật .edmx)
                /*
                var newLead = new PartnerLeads 
                {
                    RestaurantName = model.RestaurantName,
                    ContactName = model.ContactName,
                    Phone = model.ContactPhone,
                    Email = model.Email,
                    // ... (map các trường khác) ...
                    SubmittedDate = DateTime.Now,
                    Status = "Mới"
                };
                db.PartnerLeads.Add(newLead);
                db.SaveChanges();
                */

                // === GỬI EMAIL CHO ADMIN ===
                var adminEmail = ConfigurationManager.AppSettings["FromEmailAddress"] ?? "phamhuynhduyphong0308@gmail.com";
                string subject = "Đối tác nhà hàng MỚI đăng ký: " + model.RestaurantName;
                StringBuilder body = new StringBuilder();
                body.AppendLine("Có một nhà hàng mới vừa đăng ký hợp tác:");
                body.AppendLine("----------------------------------------");
                body.AppendLine($"Tên nhà hàng: {model.RestaurantName}");
                body.AppendLine($"Người liên hệ: {model.ContactName} ({model.ContactRole})");
                body.AppendLine($"Email: {model.Email}");
                body.AppendLine($"SĐT: {model.ContactPhone}");
                body.AppendLine("----------------------------------------");

                // (Hãy đảm bảo bạn đã tạo EmailHelper.cs)
                // EmailHelper.SendEmail(adminEmail, subject, body.ToString());


                TempData["SuccessMessage"] = "Gửi thông tin thành công! Chúng tôi sẽ liên hệ với bạn sớm nhất có thể.";
                return RedirectToAction("Index"); // Quay về trang Giới thiệu (Index)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi gửi thông tin. Vui lòng thử lại.";
                return View(model); // Quay lại form và báo lỗi
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // Thêm vào Controllers/BusinessController.cs

        [HttpGet]
        public ActionResult Login()
        {
            // Chúng ta trả về View "Login" và gửi một model "Users" rỗng
            // giống hệt như trang Login của Khách hàng.
            return View(new SmartTable.Models.Users());
        }
    }

}