using SmartTable.Filters;
using SmartTable.Models;
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
                // Không tin hoàn toàn ContentType, nhưng vẫn check để lọc bớt
                errorMessage = "Kiểu nội dung file không hợp lệ.";
                return false;
            }

            return true;
        }


        // Helper: Upload file (đã có validate ảnh)
        private string UploadFile(HttpPostedFileBase file, string fileName, string subFolder = "")
        {
            try
            {
                string error;
                if (!ValidateImage(file, out error))
                {
                    // Có thể lưu ModelState/TempData nếu muốn show ra View
                    // Ở controller khác anh có thể dùng ModelState.AddModelError(...)
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

                // Trả về đường dẫn để lưu DB (VD: /Content/Images/Restaurants/abc.jpg)
                return relativeFolder.Replace("~", "") + fileName;
            }
            catch
            {
                // TODO: log lỗi nếu cần
                return null;
            }
        }

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

            // TODO: Khi có bảng Bookings, thay 0 bằng số liệu thực
            ViewBag.TodayReservationCount = 0;
            return View(restaurant);
        }

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
            HttpPostedFileBase uploadedImage,              // Ảnh đại diện
            IEnumerable<HttpPostedFileBase> viewImages,    // Ảnh gallery
            int[] DeleteImages)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (userId == null)
                return RedirectToAction("Login", "Account");

            // Nếu validation fail: load lại dữ liệu cũ để view không vỡ
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

            // 2. Lấy dữ liệu gốc
            var originalRestaurant = db.Restaurants
                                       .Include(r => r.RestaurantImages)
                                       .FirstOrDefault(r => r.restaurant_id == model.restaurant_id);

            // Security check
            if (originalRestaurant == null || originalRestaurant.user_id != userId.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa nhà hàng này.";
                return RedirectToAction("Profile");
            }

            // 3. Cập nhật thông tin text
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

            // 4. Ảnh đại diện
            if (uploadedImage != null && uploadedImage.ContentLength > 0)
            {
                string fileName = $"main_{originalRestaurant.restaurant_id}_{Guid.NewGuid()}.jpg";
                string savePath = UploadFile(uploadedImage, fileName);
                if (savePath != null)
                {
                    originalRestaurant.Image = savePath;
                }
            }

            // 5. Xóa ảnh view được chọn
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
                            // img.image_url dạng "/Content/Images/Restaurants/xxx.jpg"
                            string physicalPath = Server.MapPath("~" + img.image_url);
                            if (System.IO.File.Exists(physicalPath))
                            {
                                System.IO.File.Delete(physicalPath);
                            }
                        }
                        catch
                        {
                            // Nếu xóa file lỗi thì bỏ qua, vẫn xóa record trong DB
                        }
                    }

                    db.RestaurantImages.Remove(img);
                }
            }

            // 6. Thêm ảnh view mới
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

            // 7. Chốt đơn (SaveChanges)
            db.Entry(originalRestaurant).State = EntityState.Modified;

            // Bảo vệ các field hệ thống / không cho business chỉnh
            db.Entry(originalRestaurant).Property(r => r.created_at).IsModified = false;
            db.Entry(originalRestaurant).Property(r => r.user_id).IsModified = false;
            db.Entry(originalRestaurant).Property(r => r.is_approved).IsModified = false;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Cập nhật thông tin nhà hàng thành công!";
            return RedirectToAction("Profile");
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
