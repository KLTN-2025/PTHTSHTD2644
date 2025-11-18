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
    [AuthorizeAdmin] // Chỉ Admin mới được truy cập Controller này
    public class RestaurantController : Controller
    {
        private Entities db = new Entities(); // Context EF

        // Danh sách các thuộc tính được phép bind từ form
        private const string BIND_PROPERTIES = "restaurant_id,user_id,name,address,description,max_tables,opening_hours,is_approved,Image,CuisineStyle,ServiceDescription,ServiceTypes,AverageBill,FloorCount,BusyHours,SlowHours,SignatureDishes,PartnershipGoal,ServicePackage,ContactName,ContactPhone,ContactRole,Website,SpaceDescription,Amenities,AmenitiesOther,SeatingType,PrivateRoomCount,NearbyLandmark";

        #region Index & Details

        // Hiển thị danh sách nhà hàng
        public ActionResult Index()
        {
            var restaurants = db.Restaurants.Include(r => r.Users).ToList();
            return View(restaurants);
        }

        // Hiển thị chi tiết một nhà hàng
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var restaurant = db.Restaurants
                .Include(r => r.Users) // Tải dữ liệu User
                .Include(r => r.RestaurantImages) // Tải danh sách ảnh view
                .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();
            return View(restaurant);
        }

        #endregion

        #region Create

        // GET: Hiển thị form tạo nhà hàng mới
        public ActionResult Create()
        {
            // DropDownList cho user là business
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email");
            return View();
        }

        // POST: Thêm nhà hàng mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = BIND_PROPERTIES)] Restaurants restaurant, string latitudeStr, string longitudeStr)
        {
            // Chuyển đổi tọa độ từ string sang double (culture invariant)
            if (!string.IsNullOrEmpty(latitudeStr))
                restaurant.latitude = double.Parse(latitudeStr, CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(longitudeStr))
                restaurant.longitude = double.Parse(longitudeStr, CultureInfo.InvariantCulture);

            if (ModelState.IsValid)
            {
                restaurant.created_at = DateTime.Now; // Thời gian tạo
                db.Restaurants.Add(restaurant);
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã thêm nhà hàng {restaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            // Nếu form không hợp lệ, trả về form với dữ liệu cũ
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        #endregion

        #region Edit

        // GET: Hiển thị form chỉnh sửa nhà hàng
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var restaurant = db.Restaurants
                .Include(r => r.RestaurantImages) // Tải danh sách ảnh view
                .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();

            // Danh sách tiện ích mặc định
            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            // DropDownList gán User
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        // POST: Chỉnh sửa nhà hàng
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

            // 1. Chuyển đổi latitude/longitude từ string sang double và validate
            bool latValid = double.TryParse(latitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out latValue);
            bool lngValid = double.TryParse(longitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lngValue);

            restaurant.latitude = latValid ? (double?)latValue : null;
            restaurant.longitude = lngValid ? (double?)lngValue : null;

            if (!latValid && !string.IsNullOrEmpty(latitudeStr))
                ModelState.AddModelError("latitudeStr", "Vĩ độ phải là giá trị số (dùng dấu chấm).");

            if (!lngValid && !string.IsNullOrEmpty(longitudeStr))
                ModelState.AddModelError("longitudeStr", "Kinh độ phải là giá trị số (dùng dấu chấm).");

            // 2. Xử lý tiện ích (Amenities)
            List<string> amenityList = new List<string>();
            if (AmenitiesCheckbox != null && AmenitiesCheckbox.Length > 0)
            {
                var selectedAmenities = AmenitiesCheckbox.Where(a => a != "false" && !string.IsNullOrWhiteSpace(a));
                amenityList.AddRange(selectedAmenities);
            }
            if (!string.IsNullOrEmpty(restaurant.AmenitiesOther))
                amenityList.Add($"Khác: {restaurant.AmenitiesOther}");
            restaurant.Amenities = amenityList.Count > 0 ? string.Join(", ", amenityList) : null;

            if (ModelState.IsValid)
            {
                // 3. Lấy đối tượng gốc từ DB để EF track entity
                var originalRestaurant = db.Restaurants
                    .Include(r => r.RestaurantImages)
                    .FirstOrDefault(r => r.restaurant_id == restaurant.restaurant_id);

                if (originalRestaurant == null)
                    return HttpNotFound();

                // 4. Cập nhật tất cả giá trị từ form vào đối tượng gốc
                db.Entry(originalRestaurant).CurrentValues.SetValues(restaurant);

                // 5. Xử lý ảnh đại diện
                if (uploadedImage != null && uploadedImage.ContentLength > 0)
                {
                    string uniqueFileName = Guid.NewGuid() + Path.GetExtension(uploadedImage.FileName);
                    string serverPath = Server.MapPath("~/Content/Images/Restaurants/");
                    if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
                    uploadedImage.SaveAs(Path.Combine(serverPath, uniqueFileName));

                    originalRestaurant.Image = "/Content/Images/Restaurants/" + uniqueFileName;
                }
                else
                {
                    // Giữ nguyên ảnh cũ nếu không upload
                    db.Entry(originalRestaurant).Property(r => r.Image).IsModified = false;
                }

                // Ngăn không thay đổi trường hệ thống
                db.Entry(originalRestaurant).Property(r => r.created_at).IsModified = false;

                // 6. Xóa ảnh view cũ nếu có chọn
                if (DeleteImages != null && DeleteImages.Length > 0)
                {
                    foreach (var imageId in DeleteImages)
                    {
                        var imageToDelete = originalRestaurant.RestaurantImages.FirstOrDefault(i => i.image_id == imageId);
                        if (imageToDelete != null)
                        {
                            string path = Server.MapPath(imageToDelete.image_url);
                            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                            db.RestaurantImages.Remove(imageToDelete);
                        }
                    }
                }

                // 7. Upload ảnh view mới
                if (viewImages != null && viewImages.Any(f => f != null && f.ContentLength > 0))
                {
                    foreach (var file in viewImages.Where(f => f != null && f.ContentLength > 0))
                    {
                        string uniqueFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        string serverPath = Server.MapPath("~/Content/Images/Restaurants/");
                        if (!Directory.Exists(serverPath)) Directory.CreateDirectory(serverPath);
                        file.SaveAs(Path.Combine(serverPath, uniqueFileName));

                        db.RestaurantImages.Add(new RestaurantImages
                        {
                            restaurant_id = originalRestaurant.restaurant_id,
                            image_url = "/Content/Images/Restaurants/" + uniqueFileName,
                            description = "Ảnh View Chi tiết",
                        });
                    }
                }

                // 8. Lưu tất cả thay đổi
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã cập nhật nhà hàng {originalRestaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            // Nếu validation thất bại, tạo lại SelectList và danh sách tiện ích
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

        // POST: Xóa nhà hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var restaurant = db.Restaurants.Find(id);
            if (restaurant != null)
            {
                // Xóa tất cả ảnh view liên quan
                var images = db.RestaurantImages.Where(i => i.restaurant_id == id).ToList();
                foreach (var img in images)
                {
                    string path = Server.MapPath(img.image_url);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
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

        // Chuyển độ sang radian
        private double ToRadians(double degree) => degree * Math.PI / 180;

        // Tính khoảng cách 2 điểm địa lý theo km
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371; // bán kính Trái đất
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Pow(Math.Sin(dLon / 2), 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // API trả về danh sách nhà hàng gần một tọa độ
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
