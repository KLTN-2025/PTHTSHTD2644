using SmartTable.Filters;
using SmartTable.Models;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Configuration;
using System.Collections.Generic;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] // Chỉ Admin được truy cập
    public class RestaurantController : Controller
    {
        private Entities db = new Entities();

        // Danh sách thuộc tính được phép Bind/Chỉnh sửa. 
        private const string BIND_PROPERTIES = "restaurant_id,user_id,name,address,description,max_tables,opening_hours,is_approved,Image,CuisineStyle,ServiceDescription,ServiceTypes,AverageBill,FloorCount,BusyHours,SlowHours,SignatureDishes,PartnershipGoal,ServicePackage,ContactName,ContactPhone,ContactRole,Website,SpaceDescription,Amenities,AmenitiesOther,SeatingType,PrivateRoomCount,NearbyLandmark"; // <-- ĐÃ THÊM AMENITIESOTHER

        // GET: Admin/Restaurant/Index
        public ActionResult Index()
        {
            var restaurants = db.Restaurants.Include(r => r.Users).ToList();
            return View(restaurants);
        }

        // GET: Admin/Restaurant/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Restaurants restaurant = db.Restaurants
                                       .Include(r => r.Users)
                                       .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();
            return View(restaurant);
        }

        // GET: Admin/Restaurant/Create
        public ActionResult Create()
        {
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email");
            return View();
        }

        // POST: Admin/Restaurant/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = BIND_PROPERTIES)] Restaurants restaurant, string latitudeStr, string longitudeStr)
        {
            // 1. Xử lý tọa độ từ chuỗi sang double
            if (!string.IsNullOrEmpty(latitudeStr))
            {
                restaurant.latitude = Convert.ToDouble(latitudeStr, CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(longitudeStr))
            {
                restaurant.longitude = Convert.ToDouble(longitudeStr, CultureInfo.InvariantCulture);
            }

            if (ModelState.IsValid)
            {
                restaurant.created_at = System.DateTime.Now;
                db.Restaurants.Add(restaurant);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Đã thêm nhà hàng {restaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        // GET: Admin/Restaurant/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Restaurants restaurant = db.Restaurants.Find(id);

            if (restaurant == null) return HttpNotFound();

            ViewBag.AvailableAmenities = new List<string>
            {
                "Karaoke riêng", "Karaoke chung", "Tivi/Máy chiếu",
                "Loa mic", "Khu vui chơi trẻ em", "Ghế trẻ em", "VAT"
            };

            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        // POST: Admin/Restaurant/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(
            [Bind(Include = BIND_PROPERTIES)] Restaurants restaurant,
            string latitudeStr,
            string longitudeStr,
            string[] AmenitiesCheckbox
        )
        {
            double latValue;
            double lngValue;

            // 1. SỬ DỤNG TRYPARSE (Fix lỗi tọa độ)
            bool latValid = double.TryParse(latitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out latValue);
            bool lngValid = double.TryParse(longitudeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lngValue);

            // 2. GÁN GIÁ TRỊ TỌA ĐỘ VÀ KIỂM TRA VALIDATION
            restaurant.latitude = latValid ? (double?)latValue : null;
            restaurant.longitude = lngValid ? (double?)lngValue : null;

            if (!latValid && !string.IsNullOrEmpty(latitudeStr))
            {
                ModelState.AddModelError("latitudeStr", "Vĩ độ phải là giá trị số (dùng dấu chấm).");
            }
            if (!lngValid && !string.IsNullOrEmpty(longitudeStr))
            {
                ModelState.AddModelError("longitudeStr", "Kinh độ phải là giá trị số (dùng dấu chấm).");
            }


            // 3. FIX LỖI TIỆN ÍCH: Xử lý và Ghép nối Checkbox và Text khác
            List<string> amenityList = new List<string>();

            if (AmenitiesCheckbox != null && AmenitiesCheckbox.Length > 0)
            {
                var selectedAmenities = AmenitiesCheckbox.Where(a => a != "false" && !string.IsNullOrWhiteSpace(a));
                amenityList.AddRange(selectedAmenities);
            }

            if (!string.IsNullOrEmpty(restaurant.AmenitiesOther)) // Đã thêm BIND_PROPERTY cho AmenitiesOther
            {
                amenityList.Add($"Khác: {restaurant.AmenitiesOther}");
            }

            // Gán chuỗi kết quả vào cột Amenities
            restaurant.Amenities = amenityList.Count > 0 ? string.Join(", ", amenityList) : null;


            // 4. Tiến hành lưu
            if (ModelState.IsValid)
            {
                db.Entry(restaurant).State = EntityState.Modified;
                db.Entry(restaurant).Property(r => r.created_at).IsModified = false;

                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã cập nhật nhà hàng {restaurant.name} thành công.";
                return RedirectToAction("Index");
            }

            // Nếu Model State không hợp lệ, tạo lại SelectList
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        // POST: Admin/Restaurant/Delete/5 (Chỉ dùng POST để xóa)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            Restaurants restaurant = db.Restaurants.Find(id);
            if (restaurant != null)
            {
                db.Restaurants.Remove(restaurant);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Đã xóa nhà hàng {restaurant.name} thành công.";
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- HÀM HỖ TRỢ MAP ---
        private double ToRadians(double degree)
        {
            return degree * Math.PI / 180;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
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
            var allRestaurants = db.Restaurants
                .Where(r => r.is_approved == true)
                .ToList()
                .Where(r => r.latitude != null && r.longitude != null) // Lọc an toàn sau ToList()
                .ToList();

            var nearbyRestaurants = allRestaurants
                .Select(r => new
                {
                    r.restaurant_id,
                    r.name,
                    r.address,
                    latitude = (double)r.latitude.Value,
                    longitude = (double)r.longitude.Value,
                    distanceKm = CalculateDistance(lat, lng, (double)r.latitude.Value, (double)r.longitude.Value)
                })
                .Where(r => r.distanceKm <= radiusKm)
                .OrderBy(r => r.distanceKm)
                .ToList();

            return Json(nearbyRestaurants, JsonRequestBehavior.AllowGet);
        }
    }
}