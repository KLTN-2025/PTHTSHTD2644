using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;   // <-- THÊM DÒNG NÀY
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class BusinessHomeController : Controller
    {
        private readonly Entities db = new Entities();

        // Helper: Lấy user_id hiện tại an toàn
        private int? GetCurrentUserId()
        {
            if (Session["user_id"] == null) return null;
            return Convert.ToInt32(Session["user_id"]);
        }

        // Helper: Kiểm tra role là business
        private bool IsBusiness()
        {
            return Session["role"] != null && Session["role"].ToString() == "business";
        }

        // Giới hạn & whitelist cho file ảnh
        private const int MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        // Kiểm tra file ảnh hợp lệ
        private bool ValidateImage(HttpPostedFileBase file, out string errorMessage)
        {
            errorMessage = null;

            if (file == null || file.ContentLength <= 0)
            {
                errorMessage = "File tải lên không hợp lệ.";
                return false;
            }

            if (file.ContentLength > MaxImageSizeBytes)
            {
                errorMessage = "Kích thước ảnh quá lớn (tối đa 5MB).";
                return false;
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext.ToLower()))
            {
                errorMessage = "Định dạng file không được hỗ trợ. Chỉ chấp nhận: JPG, PNG, WEBP.";
                return false;
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                errorMessage = "Kiểu nội dung file không hợp lệ.";
                return false;
            }

            return true;
        }

        // Helper: Upload file 
        private string UploadFile(HttpPostedFileBase file, string fileName, string subFolder = "")
        {
            try
            {
                string error;
                if (!ValidateImage(file, out error))
                {
                    return null;
                }

                string relativeFolder = "~/Content/Images/Restaurants/" +
                                        (string.IsNullOrEmpty(subFolder) ? "" : subFolder + "/");
                string physicalFolder = Server.MapPath(relativeFolder);

                if (!Directory.Exists(physicalFolder))
                {
                    Directory.CreateDirectory(physicalFolder);
                }

                string physicalPath = Path.Combine(physicalFolder, fileName);
                file.SaveAs(physicalPath);

                return relativeFolder.Replace("~", "") + fileName;
            }
            catch
            {
                return null;
            }
        }

        // ================== DASHBOARD BUSINESS ==================
        // [GET] /BusinessHome/Index
        public ActionResult Index()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var restaurant = db.Restaurants.FirstOrDefault(r => r.user_id == userId.Value);

            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Tài khoản của bạn chưa được liên kết với nhà hàng nào. Vui lòng liên hệ Admin để hoàn tất quá trình thiết lập.";
                return View((Restaurants)null);
            }

            // ====== LƯỢT ĐẶT BÀN HÔM NAY ======
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayReservationCount = db.Bookings
                .Where(b => b.restaurant_id == restaurant.restaurant_id
                            && b.booking_time >= today
                            && b.booking_time < tomorrow
                            && b.status != "Đã hủy")
                .Count();

            ViewBag.TodayReservationCount = todayReservationCount;

            return View(restaurant);
        }

        // ================== TRANG PROFLE NHÀ HÀNG ==================
        // [GET] /BusinessHome/Profile
        public ActionResult Profile()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var restaurant = db.Restaurants
                               .Include(r => r.RestaurantImages)
                               .FirstOrDefault(r => r.user_id == userId.Value);

            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Chưa có nhà hàng nào liên kết với tài khoản này.";
                return View((Restaurants)null);
            }

            return View(restaurant);
        }

        // [GET] /BusinessHome/EditRestaurant/{id}
        [HttpGet]
        public ActionResult EditRestaurant(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var restaurant = db.Restaurants
                               .Include(r => r.RestaurantImages)
                               .FirstOrDefault(r => r.restaurant_id == id);

            // Security: chỉ cho phép chỉnh sửa nhà hàng thuộc user hiện tại
            if (restaurant == null || restaurant.user_id != userId.Value)
            {
                return HttpNotFound();
            }

            // Danh sách tiện ích cho view
            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            return View(restaurant);
        }

        // [POST] /BusinessHome/EditRestaurant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditRestaurant(
            Restaurants model,
            string latitudeStr,
            string longitudeStr,
            string[] AmenitiesCheckbox,
            HttpPostedFileBase uploadedImage,             
            IEnumerable<HttpPostedFileBase> viewImages,    
            int[] DeleteImages)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                var currentDbItem = db.Restaurants
                                      .Include(r => r.RestaurantImages)
                                      .FirstOrDefault(r => r.restaurant_id == model.restaurant_id);

                if (currentDbItem != null)
                {
                    model.RestaurantImages = currentDbItem.RestaurantImages;
                    model.Image = currentDbItem.Image;
                }

                ViewBag.AvailableAmenities = new List<string>
                {
                    "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                    "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
                };

                return View(model);
            }

            var originalRestaurant = db.Restaurants
                                       .Include(r => r.RestaurantImages)
                                       .FirstOrDefault(r => r.restaurant_id == model.restaurant_id);

            if (originalRestaurant == null || originalRestaurant.user_id != userId.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa nhà hàng này.";
                return RedirectToAction("Profile");
            }

            // Cập nhật thông tin text
            originalRestaurant.name = model.name;
            originalRestaurant.address = model.address;
            originalRestaurant.Website = model.Website;

            originalRestaurant.ContactName = model.ContactName;
            originalRestaurant.ContactPhone = model.ContactPhone;
            originalRestaurant.ContactRole = model.ContactRole;
            originalRestaurant.PartnershipGoal = model.PartnershipGoal;
            originalRestaurant.ServicePackage = model.ServicePackage;

            originalRestaurant.description = model.description;
            originalRestaurant.SpaceDescription = model.SpaceDescription;

            originalRestaurant.CuisineStyle = model.CuisineStyle;
            originalRestaurant.ServiceDescription = model.ServiceDescription;
            originalRestaurant.ServiceTypes = model.ServiceTypes;
            originalRestaurant.AverageBill = model.AverageBill;
            originalRestaurant.SignatureDishes = model.SignatureDishes;

            originalRestaurant.opening_hours = model.opening_hours;
            originalRestaurant.max_tables = model.max_tables;
            originalRestaurant.FloorCount = model.FloorCount;
            originalRestaurant.BusyHours = model.BusyHours;
            originalRestaurant.SlowHours = model.SlowHours;
            originalRestaurant.PrivateRoomCount = model.PrivateRoomCount;
            originalRestaurant.SeatingType = model.SeatingType;
            originalRestaurant.NearbyLandmark = model.NearbyLandmark;

            originalRestaurant.AmenitiesOther = model.AmenitiesOther;

            // Amenities Checkbox
            var amenityList = new List<string>();
            if (AmenitiesCheckbox != null && AmenitiesCheckbox.Any())
            {
                amenityList.AddRange(AmenitiesCheckbox.Where(a => a != "false"));
            }
            originalRestaurant.Amenities = amenityList.Any()
                ? string.Join(", ", amenityList)
                : null;

            // Tọa độ
            if (!string.IsNullOrWhiteSpace(latitudeStr) &&
                double.TryParse(latitudeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat))
            {
                originalRestaurant.latitude = lat;
            }

            if (!string.IsNullOrWhiteSpace(longitudeStr) &&
                double.TryParse(longitudeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
            {
                originalRestaurant.longitude = lng;
            }

            // Ảnh đại diện
            if (uploadedImage != null && uploadedImage.ContentLength > 0)
            {
                string fileName = $"main_{originalRestaurant.restaurant_id}_{Guid.NewGuid()}.jpg";
                string savePath = UploadFile(uploadedImage, fileName);
                if (savePath != null)
                {
                    originalRestaurant.Image = savePath;
                }
            }

            // Xóa ảnh view
            if (DeleteImages != null && DeleteImages.Length > 0)
            {
                var imagesToDelete = originalRestaurant.RestaurantImages
                    .Where(i => DeleteImages.Contains(i.image_id))
                    .ToList();

                foreach (var img in imagesToDelete)
                {
                    if (!string.IsNullOrEmpty(img.image_url))
                    {
                        try
                        {
                            string physicalPath = Server.MapPath("~" + img.image_url);
                            if (System.IO.File.Exists(physicalPath))
                            {
                                System.IO.File.Delete(physicalPath);
                            }
                        }
                        catch
                        {
                        }
                    }

                    db.RestaurantImages.Remove(img);
                }
            }

            // Thêm ảnh view mới
            if (viewImages != null)
            {
                foreach (var file in viewImages)
                {
                    if (file == null || file.ContentLength <= 0) continue;

                    string fileName = $"gallery_{originalRestaurant.restaurant_id}_{Guid.NewGuid()}.jpg";
                    string savePath = UploadFile(file, fileName, "Gallery");

                    if (savePath != null)
                    {
                        originalRestaurant.RestaurantImages.Add(new RestaurantImages
                        {
                            restaurant_id = originalRestaurant.restaurant_id,
                            image_url = savePath,
                            description = "Ảnh View"
                        });
                    }
                }
            }

            db.Entry(originalRestaurant).State = EntityState.Modified;
            db.Entry(originalRestaurant).Property(r => r.created_at).IsModified = false;
            db.Entry(originalRestaurant).Property(r => r.user_id).IsModified = false;
            db.Entry(originalRestaurant).Property(r => r.is_approved).IsModified = false;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Cập nhật thông tin nhà hàng thành công!";
            return RedirectToAction("Profile");
        }

        // ================== DOANH NGHIỆP QUẢN LÝ ĐẶT BÀN ==================
        // [GET] /BusinessHome/Bookings
        [HttpGet]
        public ActionResult Bookings(string statusFilter = null)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var query = db.Bookings
                          .Include(b => b.Restaurants)
                          .Include(b => b.Users)
                          .Include(b => b.Payments)
                          .Where(b => b.Restaurants != null &&
                                      b.Restaurants.user_id == userId.Value);

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(b => b.status == statusFilter);
            }

            var bookingList = query
                .OrderByDescending(b => b.booking_time)   
                .ThenByDescending(b => b.booking_id)      
                .ToList();

            var list = new List<BusinessBookingItemViewModel>();

            foreach (var b in bookingList)
            {
                var lastPayment = b.Payments
                                   .OrderByDescending(p => p.payment_id)
                                   .FirstOrDefault();

                decimal? depositAmount = null;
                string paymentStatus = "Chưa thanh toán";

                if (lastPayment != null)
                {
                    depositAmount = lastPayment.amount;
                    paymentStatus = string.IsNullOrEmpty(lastPayment.status)
                        ? "Chưa cập nhật"
                        : lastPayment.status;
                }

                var vm = new BusinessBookingItemViewModel
                {
                    BookingId = b.booking_id,
                    RestaurantName = b.Restaurants != null ? b.Restaurants.name : "Nhà hàng",
                    RestaurantAddress = b.Restaurants != null ? b.Restaurants.address : "",
                    RestaurantImage = (b.Restaurants != null && !string.IsNullOrEmpty(b.Restaurants.Image))
                                      ? b.Restaurants.Image
                                      : "https://via.placeholder.com/120",

                    CustomerName = b.Users != null ? b.Users.full_name : "Khách lẻ",
                    CustomerPhone = b.Users != null ? b.Users.phone : "",

                    BookingTime = b.booking_time,
                    NumberOfGuests = b.number_of_guests,
                    SpecialRequest = b.special_request,

                    BookingStatus = string.IsNullOrEmpty(b.status) ? "Chờ xác nhận" : b.status,
                    DepositAmount = depositAmount,
                    PaymentStatus = paymentStatus,

                    CancelReason = b.cancel_reason
                };

                list.Add(vm);
            }

            ViewBag.StatusFilter = statusFilter;
            return View("Bookings", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int bookingId, string cancelReason)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .FirstOrDefault(b => b.booking_id == bookingId &&
                                                 b.Restaurants.user_id == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn hoặc bạn không có quyền.";
                return RedirectToAction("Bookings");
            }

            booking.status = "Đã hủy";

            booking.cancel_reason = string.IsNullOrWhiteSpace(cancelReason)
                ? "Doanh nghiệp hủy, không ghi rõ lý do."
                : cancelReason.Trim();

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hủy đặt bàn #{booking.booking_id}.";
            return RedirectToAction("Bookings");
        }
        // [POST] /BusinessHome/ConfirmPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(int bookingId)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Payments)
                            .FirstOrDefault(b => b.booking_id == bookingId &&
                                                 b.Restaurants.user_id == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn hoặc bạn không có quyền.";
                return RedirectToAction("Bookings");
            }

            var lastPayment = booking.Payments
                                     .OrderByDescending(p => p.payment_id)
                                     .FirstOrDefault();

            if (lastPayment == null)
            {
                lastPayment = new Payments
                {
                    booking_id = booking.booking_id,
                    amount = 0, 
                    payment_method = "Chuyển khoản / Doanh nghiệp xác nhận",
                    status = "Đã thanh toán (doanh nghiệp xác nhận)",
                    transaction_id = Guid.NewGuid().ToString()
                };
                db.Payments.Add(lastPayment);
            }
            else
            {
                lastPayment.status = "Đã thanh toán (doanh nghiệp xác nhận)";
            }

            booking.status = "Đã cọc thành công";

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xác nhận thanh toán cho mã đặt bàn #{booking.booking_id}.";
            return RedirectToAction("Bookings");
        }

        // ================== DISPOSE ==================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult Statistics()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var bookings = db.Bookings
                             .Include(b => b.Restaurants)
                             .Include(b => b.Payments)
                             .Where(b => b.Restaurants != null &&
                                         b.Restaurants.user_id == userId.Value)
                             .ToList();

            var vm = new StatisticsViewModel();

            vm.TotalBookings = bookings.Count;

            var paidPayments = bookings
                .SelectMany(b => b.Payments)
                .Where(p => !string.IsNullOrEmpty(p.status) &&
                            (p.status.Contains("Đã thanh toán") ||
                             p.status.Contains("VNPAY")));

            vm.TotalDeposit = paidPayments
                .Select(p => (decimal?)p.amount)
                .Sum() ?? 0;

            vm.RestaurantStats = bookings
                .GroupBy(b => new { b.restaurant_id, b.Restaurants.name })
                .Select(g => new RestaurantStatistics
                {
                    RestaurantId = g.Key.restaurant_id ?? 0,
                    RestaurantName = g.Key.name,
                    BookingCount = g.Count(),
                    DepositAmount = g.SelectMany(b => b.Payments)
                                     .Where(p => !string.IsNullOrEmpty(p.status) &&
                                                 (p.status.Contains("Đã thanh toán") ||
                                                  p.status.Contains("VNPAY")))
                                     .Select(p => (decimal?)p.amount)
                                     .Sum() ?? 0
                })
                .OrderByDescending(r => r.BookingCount) 
                .ToList();

            vm.DailyStats = bookings
            .GroupBy(b => b.booking_time.Date) 
            .Select(g => new DailyStatistics
            {
                Date = g.Key,                     
                BookingCount = g.Count(),
                DepositAmount = g.SelectMany(b => b.Payments)
                                 .Where(p => !string.IsNullOrEmpty(p.status) &&
                                             (p.status.Contains("Đã thanh toán") ||
                                              p.status.Contains("VNPAY")))
                                 .Select(p => (decimal?)p.amount)
                                 .Sum() ?? 0
            })
            .OrderByDescending(d => d.Date)
            .ToList();

            return View(vm);  
        }
    }
}
