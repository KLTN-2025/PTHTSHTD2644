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
        private Entities db = new Entities(); 

        public ActionResult Index()
        {
            
            return View();
        }

        [HttpGet]
        public ActionResult RegisterPartner()
        {
            var model = new PartnerRegistrationViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterPartner(PartnerRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.";
                return View("RegisterPartner", model);
            }

            try
            {

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
                    OpeningDate = model.OpeningDate ?? DateTime.Today,
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


                // ===GỬI EMAIL CHO ADMIN ===
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

                EmailHelper.SendEmail(adminEmail, subject, body.ToString());

                TempData["SuccessMessage"] = "Gửi thông tin thành công! Chúng tôi sẽ liên hệ với bạn sớm nhất có thể.";
                return RedirectToAction("Index"); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LỖI RegisterPartner: " + ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi gửi thông tin. Vui lòng thử lại.";
                return View("RegisterPartner", model); 
            }
        } 

        // [GET] /Business/Login
        [HttpGet]
        public ActionResult Login()
        {
            return View(new SmartTable.Models.Users());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

    } 
} 