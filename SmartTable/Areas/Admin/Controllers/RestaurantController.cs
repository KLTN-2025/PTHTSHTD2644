using SmartTable.Filters;
using SmartTable.Models;
using System.Data.Entity; 
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System;


namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] // Chỉ Admin được truy cập
    public class RestaurantController : Controller
    {
        private Entities db = new Entities();

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

            // Đã tắt Proxy, nên chỉ cần Include là đủ
            Restaurants restaurant = db.Restaurants
                                       .Include(r => r.Users)
                                       .FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null) return HttpNotFound();

            return View(restaurant); // Trả về đối tượng gốc (non-proxy)
        }


        // GET: Admin/Restaurant/Create
        public ActionResult Create()
        {
            // Trả về danh sách Users để Admin chọn Chủ sở hữu
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email");
            return View();
        }

        // POST: Admin/Restaurant/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "restaurant_id,user_id,name,address,latitude,longitude,description,max_tables,opening_hours,is_approved,Image")] Restaurants restaurant)
        {
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
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            // FIX PROXY: Dùng AsNoTracking() cho Edit cũng giúp ngăn lỗi
            Restaurants restaurant = db.Restaurants.AsNoTracking().FirstOrDefault(r => r.restaurant_id == id);

            if (restaurant == null)
            {
                return HttpNotFound();
            }
            ViewBag.user_id = new SelectList(db.Users.Where(u => u.role == "business"), "user_id", "email", restaurant.user_id);
            return View(restaurant);
        }

        // POST: Admin/Restaurant/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(
    [Bind(Include = "restaurant_id,user_id,name,address,description,max_tables,opening_hours,is_approved,Image")] Restaurants restaurant,
    string latitudeStr, // <-- Nhận giá trị Vĩ độ dưới dạng chuỗi
    string longitudeStr // <-- Nhận giá trị Kinh độ dưới dạng chuỗi
)
        {
            // 1. CHUYỂN ĐỔI CHUỖI VỀ DECIMAL (Dùng Culture Invariant - Dấu chấm)
            if (!string.IsNullOrEmpty(latitudeStr))
            {
                restaurant.latitude = Convert.ToDecimal(latitudeStr, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(longitudeStr))
            {
                restaurant.longitude = Convert.ToDecimal(longitudeStr, System.Globalization.CultureInfo.InvariantCulture);
            }

            // 2. Tiếp tục validation và lưu
            if (ModelState.IsValid)
            {
                db.Entry(restaurant).State = EntityState.Modified;
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
                // Xử lý liên kết
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

        // Hàm hỗ trợ chuyển đổi từ độ sang radian (cần cho Haversine)
        private double ToRadians(double degree)
        {
            return degree * Math.PI / 180;
        }

        // Hàm tính khoảng cách Haversine giữa 2 tọa độ (trả về km)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // ... (Code Haversine giữ nguyên) ...
            var R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Pow(Math.Sin(dLon / 2), 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // Khoảng cách theo km
        }

        // GET: Admin/Restaurant/GetNearbyMapData?lat=...&lng=...
        [HttpGet]
        public JsonResult GetNearbyMapData(double lat, double lng, double radiusKm = 5)
        {
            // Lấy tất cả nhà hàng đã được duyệt có tọa độ
            var allRestaurants = db.Restaurants.AsNoTracking()
                .Where(r => r.latitude.HasValue && r.longitude.HasValue && r.is_approved == true)
                .ToList();

            var nearbyRestaurants = allRestaurants
                .Select(r => new
                {
                    r.restaurant_id,
                    r.name,
                    r.address,
            // SỬA LỖI: Ép kiểu sang double để JSON nhận đúng định dạng số
            latitude = (double)r.latitude.Value,
                    longitude = (double)r.longitude.Value,
            // Tính khoảng cách
            distanceKm = CalculateDistance(lat, lng, (double)r.latitude.Value, (double)r.longitude.Value)
                })
                .Where(r => r.distanceKm <= radiusKm)
                .OrderBy(r => r.distanceKm)
                .ToList();

            return Json(nearbyRestaurants, JsonRequestBehavior.AllowGet);
        }

    }
}