using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
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

        private const decimal DEPOSIT_PER_GUEST = 50000m;
        private const int MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        // ================== SESSION HELPERS ==================
        private int? GetCurrentUserId()
        {
            if (Session["user_id"] == null) return null;
            int id;
            return int.TryParse(Session["user_id"].ToString(), out id) ? (int?)id : null;
        }

        private bool IsBusiness()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString().Trim();
            return role.Equals("business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Business", StringComparison.OrdinalIgnoreCase);
        }

        // ================== UPLOAD HELPERS ==================
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
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext.ToLowerInvariant()))
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

        private string UploadFile(HttpPostedFileBase file, string fileName, string subFolder = "")
        {
            try
            {
                string error;
                if (!ValidateImage(file, out error)) return null;

                string relativeFolder = "~/Content/Images/Restaurants/" +
                                        (string.IsNullOrEmpty(subFolder) ? "" : subFolder + "/");
                string physicalFolder = Server.MapPath(relativeFolder);

                if (!Directory.Exists(physicalFolder))
                    Directory.CreateDirectory(physicalFolder);

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

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayReservationCount = db.Bookings
                .Where(b => b.restaurant_id == restaurant.restaurant_id
                            && b.booking_time >= today
                            && b.booking_time < tomorrow
                            && (b.status == null || !b.status.Contains("hủy")))
                .Count();

            ViewBag.TodayReservationCount = todayReservationCount;
            return View(restaurant);
        }

        // ================== PROFILE ==================
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

        // ================== EDIT RESTAURANT ==================
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

            if (restaurant == null || restaurant.user_id != userId.Value)
                return HttpNotFound();

            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            return View(restaurant);
        }

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

            var amenityList = new List<string>();
            if (AmenitiesCheckbox != null && AmenitiesCheckbox.Any())
                amenityList.AddRange(AmenitiesCheckbox.Where(a => a != "false"));

            originalRestaurant.Amenities = amenityList.Any() ? string.Join(", ", amenityList) : null;

            if (!string.IsNullOrWhiteSpace(latitudeStr) &&
                double.TryParse(latitudeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat))
                originalRestaurant.latitude = lat;

            if (!string.IsNullOrWhiteSpace(longitudeStr) &&
                double.TryParse(longitudeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
                originalRestaurant.longitude = lng;

            if (uploadedImage != null && uploadedImage.ContentLength > 0)
            {
                string fileName = $"main_{originalRestaurant.restaurant_id}_{Guid.NewGuid()}.jpg";
                string savePath = UploadFile(uploadedImage, fileName);
                if (savePath != null) originalRestaurant.Image = savePath;
            }

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
                                System.IO.File.Delete(physicalPath);
                        }
                        catch { }
                    }
                    db.RestaurantImages.Remove(img);
                }
            }

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

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
