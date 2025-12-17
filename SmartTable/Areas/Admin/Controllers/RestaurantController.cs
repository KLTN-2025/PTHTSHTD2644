using SmartTable.Filters;
using SmartTable.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] 
    public class RestaurantController : Controller
    {
        private Entities db = new Entities();

        private const string BIND_PROPERTIES =
            "restaurant_id,user_id," +
            "name,address,City,Area," +                
            "latitude,longitude," +
            "description,max_tables,opening_hours,is_approved,Image," +
            "CuisineStyle,ServiceDescription,ServiceTypes,AverageBill," +
            "FloorCount,BusyHours,SlowHours,SignatureDishes," +
            "PartnershipGoal,ServicePackage,ContactName,ContactPhone,ContactRole," +
            "Website,SpaceDescription,Amenities,AmenitiesOther," +
            "SeatingType,PrivateRoomCount,NearbyLandmark";


        private const int MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        
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

        #region Index & Details

        public ActionResult Index()
        {
            var restaurants = db.Restaurants.Include(r => r.Users).ToList();
            return View(restaurants);
        }

        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var restaurant = db.Restaurants
                .Include(r => r.Users) 
                .Include(r => r.RestaurantImages) 
                .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();
            return View(restaurant);
        }

        #endregion

        #region Create

        public ActionResult Create()
        {
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = BIND_PROPERTIES)] Restaurants restaurant, string latitudeStr, string longitudeStr)
        {
            if (!string.IsNullOrEmpty(latitudeStr))
                restaurant.latitude = double.Parse(latitudeStr, CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(longitudeStr))
                restaurant.longitude = double.Parse(longitudeStr, CultureInfo.InvariantCulture);

            if (ModelState.IsValid)
            {
                restaurant.created_at = DateTime.Now; 
                db.Restaurants.Add(restaurant);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã thêm nhà hàng {restaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        #endregion

        #region Edit

        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var restaurant = db.Restaurants
                .Include(r => r.RestaurantImages) 
                .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();

            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(
            [Bind(Include = BIND_PROPERTIES)] Restaurants restaurant,
            string latitudeStr,
            string longitudeStr,
            string[] AmenitiesCheckbox,
            HttpPostedFileBase uploadedImage,
            HttpPostedFileBase[] viewImages,
            int[] DeleteImages
        )
        {
            double latValue, lngValue;

            bool latValid = double.TryParse(latitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out latValue);
            bool lngValid = double.TryParse(longitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lngValue);

            restaurant.latitude = latValid ? (double?)latValue : null;
            restaurant.longitude = lngValid ? (double?)lngValue : null;

            if (!latValid && !string.IsNullOrEmpty(latitudeStr))
                ModelState.AddModelError("latitudeStr", "Vĩ độ phải là giá trị số (dùng dấu chấm).");

            if (!lngValid && !string.IsNullOrEmpty(longitudeStr))
                ModelState.AddModelError("longitudeStr", "Kinh độ phải là giá trị số (dùng dấu chấm).");

            List<string> amenityList = new List<string>();
            if (AmenitiesCheckbox != null && AmenitiesCheckbox.Length > 0)
            {
                var selectedAmenities = AmenitiesCheckbox
                    .Where(a => a != "false" && !string.IsNullOrWhiteSpace(a));
                amenityList.AddRange(selectedAmenities);
            }
            if (!string.IsNullOrEmpty(restaurant.AmenitiesOther))
                amenityList.Add($"Khác: {restaurant.AmenitiesOther}");

            restaurant.Amenities = amenityList.Count > 0 ? string.Join(", ", amenityList) : null;

            if (ModelState.IsValid)
            {
                var originalRestaurant = db.Restaurants
                    .Include(r => r.RestaurantImages)
                    .FirstOrDefault(r => r.restaurant_id == restaurant.restaurant_id);

                if (originalRestaurant == null)
                    return HttpNotFound();

                db.Entry(originalRestaurant).CurrentValues.SetValues(restaurant);

                if (uploadedImage != null && uploadedImage.ContentLength > 0)
                {
                    string fileName = $"main_{originalRestaurant.restaurant_id}_{Guid.NewGuid()}.jpg";
                    string savePath = UploadFile(uploadedImage, fileName);

                    if (savePath != null)
                    {
                        originalRestaurant.Image = savePath;
                    }
                    else
                    {
                         ModelState.AddModelError("uploadedImage", "Ảnh không hợp lệ hoặc tải lên thất bại.");
                         return View(restaurant);
                    }
                }

                db.Entry(originalRestaurant).Property(r => r.created_at).IsModified = false;

                if (DeleteImages != null && DeleteImages.Length > 0)
                {
                    foreach (var imageId in DeleteImages)
                    {
                        var imageToDelete = originalRestaurant.RestaurantImages.FirstOrDefault(i => i.image_id == imageId);
                        if (imageToDelete != null)
                        {
                            try
                            {
                                string path = Server.MapPath("~" + imageToDelete.image_url);
                                if (System.IO.File.Exists(path))
                                    System.IO.File.Delete(path);
                            }
                            catch
                            {
                            }

                            db.RestaurantImages.Remove(imageToDelete);
                        }
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

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã cập nhật nhà hàng {originalRestaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            return View(restaurant);
        }

        #endregion

        #region Delete

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var restaurant = db.Restaurants
                .Include(r => r.RestaurantImages)
                .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant != null)
            {
                var images = restaurant.RestaurantImages.ToList();
                foreach (var img in images)
                {
                    try
                    {
                        string path = Server.MapPath("~" + img.image_url);
                        if (System.IO.File.Exists(path))
                            System.IO.File.Delete(path);
                    }
                    catch
                    {
                        // ignore lỗi file
                    }

                    db.RestaurantImages.Remove(img);
                }

                db.Restaurants.Remove(restaurant);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã xóa nhà hàng {restaurant.name} thành công.";
            }
            return RedirectToAction("Index");
        }

        #endregion

        #region Helpers

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private double ToRadians(double degree) => degree * Math.PI / 180;

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; 
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Pow(Math.Sin(dLon / 2), 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        [HttpGet]
        public JsonResult GetNearbyMapData(double lat, double lng, double radiusKm = 5)
        {
            var nearby = db.Restaurants
                .Where(r => r.is_approved == true && r.latitude != null && r.longitude != null)
                .AsEnumerable()
                .Select(r => new
                {
                    r.restaurant_id,
                    r.name,
                    r.address,
                    latitude = r.latitude.Value,
                    longitude = r.longitude.Value,
                    distanceKm = CalculateDistance(lat, lng, r.latitude.Value, r.longitude.Value)
                })
                .Where(r => r.distanceKm <= radiusKm)
                .OrderBy(r => r.distanceKm)
                .ToList();

            return Json(nearby, JsonRequestBehavior.AllowGet);
        }

        #endregion
    }
}
