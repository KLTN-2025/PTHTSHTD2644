using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SmartTable.Models; // <-- Đảm bảo có
using SmartTable.Models.ViewModels; // <-- SỬA LỖI 1: Thêm using này
using System.Data.Entity;
using System.Device.Location; // <-- Đảm bảo bạn đã Add Reference System.Device

namespace SmartTable.Controllers
{
    public class HomeController : Controller
    {
        private Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        // (Trong file HomeController.cs)

        public ActionResult Index()
        {
            // Lấy danh sách nhà hàng (ví dụ: chỉ lấy những nhà hàng đã được duyệt)
            var restaurants = db.Restaurants
                                .Where(r => r.is_approved == true) // Lọc nhà hàng đã duyệt
                                .OrderBy(r => r.name) // Sắp xếp theo tên
                                .ToList();

            return View(restaurants); // Gửi danh sách nhà hàng sang View
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }

        // Sửa: Đổi 'int id' thành 'int? id' (cho phép id bị null)
        public ActionResult RestaurantDetails(int? id)
        {
            // 1. Kiểm tra xem id có giá trị không
            if (id == null)
            {
                return RedirectToAction("Index");
            }

            // 2. Tìm nhà hàng (dùng id.Value)
            var restaurant = db.Restaurants.Find(id.Value);
            if (restaurant == null)
            {
                return HttpNotFound();
            }

            // 3. Tạo ViewModel
            var viewModel = new SmartTableDetailViewModel
            {
                Restaurant = restaurant,
                // Lấy dữ liệu thật (thay vì dữ liệu giả)
                MenuItems = db.MenuItems.Where(m => m.restaurant_id == id.Value).ToList(),
                Reviews = db.Reviews.Where(r => r.restaurant_id == id.Value).Include(rev => rev.Users).ToList()
            };

            // 4. Trả về View với ViewModel
            return View(viewModel);
        }

        [HttpGet]
        public JsonResult GetNearbyRestaurants(double lat, double lng)
        {
            var userCoord = new GeoCoordinate(lat, lng);

            var restaurants = db.Restaurants
                .ToList()
                .Select(r => new {
                    Id = r.restaurant_id,
                    Name = r.name,
                    Address = r.address,

                    // SỬA LỖI 2 (CS1503): Ép kiểu (cast) từ decimal? sang double?
                    Distance = GetDistance(userCoord, (double?)r.latitude, (double?)r.longitude)
                })
                .Where(r => r.Distance != null && r.Distance < 20)
                .OrderBy(r => r.Distance)
                .Take(10)
                .ToList();

            return Json(restaurants, JsonRequestBehavior.AllowGet);
        }

        private double? GetDistance(GeoCoordinate userCoord, double? restaurantLat, double? restaurantLng)
        {
            if (!restaurantLat.HasValue || !restaurantLng.HasValue)
            {
                return null;
            }
            try
            {
                var restaurantCoord = new GeoCoordinate(restaurantLat.Value, restaurantLng.Value);
                return userCoord.GetDistanceTo(restaurantCoord) / 1000; // km
            }
            catch (Exception)
            {
                return null;
            }
        }

    } // <-- Đóng class HomeController
}