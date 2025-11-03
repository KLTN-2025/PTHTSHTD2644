using SmartTable.Models.ViewModels;
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
        private Entities db = new Entities(); // Khởi tạo DbContext

        // [GET] /Business/Index (Trang giới thiệu)
        public ActionResult Index()
        {
            // Trả về trang giới thiệu (Index.cshtml)
            // Trang này không cần model
            return View();
        }

        // [GET] /Business/RegisterPartner (Hiển thị Form)
        [HttpGet]
        public ActionResult RegisterPartner()
        {
            // Trả về trang form (RegisterPartner.cshtml)
            var model = new PartnerRegistrationViewModel();
            return View(model);
        }

        // [POST] /Business/RegisterPartner (Xử lý Form)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterPartner(PartnerRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.";
                // Quan trọng: Trả về View "RegisterPartner" chứ không phải "Index"
                return View("RegisterPartner", model);
            }

            try
            {
                // === BƯỚC 1: LƯU VÀO DATABASE ===

                string serviceTypes = (model.ServiceTypes != null) ? string.Join(", ", model.ServiceTypes) : null;
                string amenities = (model.Amenities != null) ? string.Join(", ", model.Amenities) : null;

                var newLead = new PartnerLeads
                {
                    Email = model.Email,
                    RestaurantName = model.RestaurantName,
                    City = model.City,
                    Address = model.Address,
                    BranchCount = model.BranchCount,
                    ServiceTypes = serviceTypes,
                    ServiceTypeOther = model.ServiceTypeOther,
                    ServiceDescription = model.ServiceDescription,
                    CuisineStyle = model.CuisineStyle,
                    SignatureDishes = model.SignatureDishes,
                    AverageBill = model.AverageBill,
                    AverageBillOther = model.AverageBillOther,
                    TotalSeats = model.TotalSeats,
                    FloorCount = model.FloorCount,
                    OpeningDate = model.OpeningDate.Value,
                    OpeningTime = model.OpeningTime,
                    ClosingTime = model.ClosingTime,
                    SlowHours = model.SlowHours,
                    BusyHours = model.BusyHours,
                    PartnershipGoal = model.PartnershipGoal,
                    ServicePackage = model.ServicePackage,
                    ContactName = model.ContactName,
                    ContactRole = model.ContactRole,
                    ContactPhone = model.ContactPhone,
                    Website = model.Website,
                    SpaceDescription = model.SpaceDescription,
                    SeatingType = model.SeatingType,
                    PrivateRoomCount = model.PrivateRoomCount,
                    Amenities = amenities,
                    AmenitiesOther = model.AmenitiesOther,
                    NearbyLandmark = model.NearbyLandmark,
                    PhotoLink = model.PhotoLink,
                    PreviousPartnership = model.PreviousPartnership,
                    PreviousPartnershipOther = model.PreviousPartnershipOther,
                    PreviousPartnershipName = model.PreviousPartnershipName,
                    Questions = model.Questions,
                    SubmittedDate = DateTime.Now,
                    Status = "Mới"
                };

                db.PartnerLeads.Add(newLead);
                db.SaveChanges();


                // === BƯỚC 2: GỬI EMAIL CHO ADMIN ===
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

                // (Đảm bảo bạn đã tạo file Helpers/EmailHelper.cs)
                EmailHelper.SendEmail(adminEmail, subject, body.ToString());

                // 3. Gửi thông báo thành công
                TempData["SuccessMessage"] = "Gửi thông tin thành công! Chúng tôi sẽ liên hệ với bạn sớm nhất có thể.";
                return RedirectToAction("Index"); // Quay về trang Giới thiệu (Index)
            }
            catch (Exception ex)
            {
                // 4. Xử lý nếu có lỗi
                System.Diagnostics.Debug.WriteLine("LỖI RegisterPartner: " + ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi gửi thông tin. Vui lòng thử lại.";
                return View("RegisterPartner", model); // Quay lại form và báo lỗi
            }
        } // <-- Đóng hàm RegisterPartner [POST]

        // [GET] /Business/Login
        [HttpGet]
        public ActionResult Login()
        {
            // Trả về View "Login" và gửi một model "Users" rỗng
            return View(new SmartTable.Models.Users());
        }

        // Ghi đè phương thức Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

    } // <-- Đóng class BusinessController
} // <-- Đóng namespace