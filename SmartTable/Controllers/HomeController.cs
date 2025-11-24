using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System.Data.Entity;
using System.Device.Location;

namespace SmartTable.Controllers
{
    public class HomeController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ================== HELPER DÙNG CHUNG ==================

        /// <summary>
        /// Lấy danh sách thành phố từ bảng Restaurants (đã được duyệt).
        /// Không dùng IsNullOrWhiteSpace trong LINQ to Entities.
        /// </summary>
        private List<string> GetAvailableCities()
        {
            return db.Restaurants
                     .Where(r => r.is_approved == true
                                 && r.City != null
                                 && r.City != "")
                     .Select(r => r.City)
                     .Distinct()
                     .OrderBy(c => c)
                     .ToList();
        }

        /// <summary>
        /// Tách khu vực (quận/huyện) từ address.
        /// Hàm này chỉ chạy trên RAM, nên dùng IsNullOrWhiteSpace thoải mái.
        /// Ví dụ: "Số 1 ABC, Phường X, Quận 3, TP. HCM" => "Quận 3"
        /// </summary>
        private string ExtractAreaFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var parts = address
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length > 1)
            {
                // Lấy phần đứng trước City: thường là Quận/Huyện
                return parts[parts.Length - 2];
            }

            return null;
        }

        /// <summary>
        /// Lấy danh sách khu vực (quận/huyện) theo City.
        /// </summary>
        private List<string> GetAreasByCityInternal(string city)
        {
            if (string.IsNullOrEmpty(city))
                return new List<string>();

            // Lấy address của các nhà hàng trong city đó
            var addresses = db.Restaurants
                              .Where(r => r.is_approved == true
                                          && r.City == city
                                          && r.address != null
                                          && r.address != "")
                              .Select(r => r.address)
                              .ToList(); // về RAM

            var areas = addresses
                .Select(ExtractAreaFromAddress)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            return areas;
        }

        // ================== TRANG CHÍNH ==================

        [HttpGet]
        public ActionResult Index()
        {
            var vm = new RestaurantFilterViewModel();

            // 1. Thành phố cho dropdown
            ViewBag.AvailableCities = GetAvailableCities();

            // 2. Gợi ý 9 nhà hàng mới nhất (đã duyệt)
            vm.Results = db.Restaurants
                           .Where(r => r.is_approved == true)
                           .OrderByDescending(r => r.created_at)
                           .Take(9)
                           .ToList();

            return View(vm); // View: Views/Home/Index.cshtml
        }

        // ================== TRANG KẾT QUẢ LỌC ==================

        [HttpGet]
        public ActionResult Search(RestaurantFilterViewModel filter)
        {
            if (filter == null)
                filter = new RestaurantFilterViewModel();

            // 1. Luôn nạp lại danh sách Thành phố cho dropdown
            ViewBag.AvailableCities = GetAvailableCities();

            // 2. Nếu đã chọn City, nạp danh sách Area tương ứng (cho dropdown hoặc JS xài)
            if (!string.IsNullOrEmpty(filter.City))
            {
                ViewBag.AvailableAreas = GetAreasByCityInternal(filter.City);
            }
            else
            {
                ViewBag.AvailableAreas = new List<string>();
            }

            // 3. Xây query lọc
            var query = db.Restaurants.Where(r => r.is_approved == true);

            // --- CITY ---
            if (!string.IsNullOrEmpty(filter.City))
            {
                var city = filter.City.Trim();
                query = query.Where(r => r.City == city);
            }

            // --- AREA (quận / khu vực, dựa trên address) ---
            if (!string.IsNullOrEmpty(filter.Area))
            {
                var area = filter.Area.Trim();
                query = query.Where(r => r.address != null && r.address.Contains(area));
            }

            // --- LOẠI HÌNH / NHÀ HÀNG ---
            if (!string.IsNullOrEmpty(filter.RestaurantType))
            {
                var type = filter.RestaurantType.Trim();

                query = query.Where(r =>
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(type)) ||
                    (r.ServiceTypes != null && r.ServiceTypes.Contains(type)) ||
                    (r.ServiceDescription != null && r.ServiceDescription.Contains(type)));
            }

            // --- GIÁ TRUNG BÌNH (AverageBill lưu dạng chuỗi: "Dưới 150K", "150K - 250K", ...) ---
            if (!string.IsNullOrEmpty(filter.AveragePrice))
            {
                var price = filter.AveragePrice.Trim();
                query = query.Where(r => r.AverageBill == price);
            }

            // --- MÓN ĐẶC SẮC / ĐỒ ĂN CHÍNH ---
            if (!string.IsNullOrEmpty(filter.MainDish))
            {
                var dish = filter.MainDish.Trim();
                query = query.Where(r =>
                    r.SignatureDishes != null &&
                    r.SignatureDishes.Contains(dish));
            }

            // --- PHÙ HỢP VỚI (Mô tả không gian) ---
            if (!string.IsNullOrEmpty(filter.SuitableFor))
            {
                var suit = filter.SuitableFor.Trim();
                query = query.Where(r =>
                    r.SpaceDescription != null &&
                    r.SpaceDescription.Contains(suit));
            }

            // --- TAG ẨM THỰC NGANG (Nướng / Lẩu / Buffet / Hải sản / ...) ---
            if (!string.IsNullOrEmpty(filter.CuisineTag))
            {
                var tag = filter.CuisineTag.Trim();
                query = query.Where(r =>
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(tag)) ||
                    (r.ServiceTypes != null && r.ServiceTypes.Contains(tag)) ||
                    (r.SignatureDishes != null && r.SignatureDishes.Contains(tag)));
            }

            // --- TỪ KHÓA TÌM KIẾM CHUNG ---
            if (!string.IsNullOrEmpty(filter.SearchKeyword))
            {
                var kw = filter.SearchKeyword.Trim();
                query = query.Where(r =>
                    (r.name != null && r.name.Contains(kw)) ||
                    (r.address != null && r.address.Contains(kw)) ||
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(kw)) ||
                    (r.SignatureDishes != null && r.SignatureDishes.Contains(kw)));
            }

            // 4. Lấy kết quả
            filter.Results = query
                .OrderBy(r => r.name)
                .ToList();

            // 5. Trả về view Search.cshtml
            return View("Search", filter);
        }

        // ================== API LẤY KHU VỰC (CHO AJAX) ==================

        [HttpGet]
        public JsonResult GetAreasByCity(string city)
        {
            var areas = GetAreasByCityInternal(city);
            return Json(areas, JsonRequestBehavior.AllowGet);
        }

        // ================== DETAILS (NẾU BẠN VẪN DÙNG TRONG HOME) ==================

        public ActionResult RestaurantDetails(int? id)
        {
            if (!id.HasValue)
                return RedirectToAction("Index");

            var restaurant = db.Restaurants.Find(id.Value);
            if (restaurant == null)
                return HttpNotFound();

            var vm = new SmartTableDetailViewModel
            {
                Restaurant = restaurant,
                MenuItems = db.MenuItems
                              .Where(m => m.restaurant_id == id.Value)
                              .ToList(),
                Reviews = db.Reviews
                            .Where(r => r.restaurant_id == id.Value)
                            .Include(r => r.Users)
                            .ToList()
            };

            return View(vm); // Views/Home/RestaurantDetails.cshtml
        }

        // ================== NEARBY (NẾU BẠN ĐANG DÙNG BẢN ĐỒ / VỊ TRÍ) ==================

        [HttpGet]
        public JsonResult GetNearbyRestaurants(double lat, double lng)
        {
            var userCoord = new GeoCoordinate(lat, lng);

            var restaurants = db.Restaurants
                .Where(r => r.is_approved == true)
                .ToList() // về RAM để dùng GeoCoordinate
                .Select(r => new
                {
                    Id = r.restaurant_id,
                    Name = r.name,
                    Address = r.address,
                    Distance = GetDistance(userCoord, r.latitude, r.longitude)
                })
                .Where(r => r.Distance != null && r.Distance < 20) // < 20km
                .OrderBy(r => r.Distance)
                .Take(10)
                .ToList();

            return Json(restaurants, JsonRequestBehavior.AllowGet);
        }

        private double? GetDistance(GeoCoordinate userCoord, double? restaurantLat, double? restaurantLng)
        {
            if (!restaurantLat.HasValue || !restaurantLng.HasValue) return null;

            try
            {
                var restaurantCoord = new GeoCoordinate(restaurantLat.Value, restaurantLng.Value);
                return userCoord.GetDistanceTo(restaurantCoord) / 1000.0; // km
            }
            catch
            {
                return null;
            }
        }

        // ================== HEADER SEARCH BAR (NẾU DÙNG PARTIAL) ==================

        [ChildActionOnly]
        public ActionResult HeaderSearchBar()
        {
            ViewBag.AvailableCities = GetAvailableCities();
            return PartialView("_HeaderSearchBar");
        }
    }
}
