using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System.Data.Entity;
using System.Device.Location;
using System.Globalization;
using System.Text;

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

        private static string NormalizePlaceName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            s = s.Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            s = s.TrimEnd('.', ',', ';', ':', '-');

            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            for (int i = 0; i < formD.Length; i++)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(formD[i]);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(formD[i]);
            }

            var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
            noDiacritics = noDiacritics.Replace("đ", "d").Replace("Đ", "D");
            return noDiacritics.ToLowerInvariant();
        }

        private static string ToTitleCaseVi(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            s = s.TrimEnd('.', ',', ';', ':', '-');
            return CultureInfo.GetCultureInfo("vi-VN").TextInfo.ToTitleCase(s.ToLower());
        }

        private string ExtractAreaFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var parts = address
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (parts.Length >= 3)
                return parts[parts.Length - 2];

            if (parts.Length == 2)
                return parts[0];

            return null;
        }

        private List<string> GetAvailableCities()
        {
            var cities = db.Restaurants
                .Where(r => r.is_approved == true && r.City != null && r.City != "")
                .Select(r => r.City)
                .ToList();

            var result = cities
                .Select(c => new { Raw = c, Key = NormalizePlaceName(c) })
                .Where(x => x.Key != "")
                .GroupBy(x => x.Key)
                .Select(g => ToTitleCaseVi(g.First().Raw))
                .OrderBy(x => x)
                .ToList();

            return result;
        }

        private List<string> GetCityVariants(string selectedCity)
        {
            var key = NormalizePlaceName(selectedCity);
            if (string.IsNullOrWhiteSpace(key)) return new List<string>();

            var allCities = db.Restaurants
                .Where(r => r.is_approved == true && r.City != null && r.City != "")
                .Select(r => r.City)
                .ToList();

            var variants = allCities
                .Where(c => NormalizePlaceName(c) == key)
                .Distinct()
                .ToList();

            return variants;
        }

        private List<string> GetAreasByCityInternal(string city)
        {
            if (string.IsNullOrWhiteSpace(city))
                return new List<string>();

            var cityKey = NormalizePlaceName(city);
            if (string.IsNullOrWhiteSpace(cityKey))
                return new List<string>();

            var candidates = db.Restaurants
                .Where(r => r.is_approved == true && r.City != null && r.City != "")
                .Select(r => new { r.City, r.Area, r.address })
                .ToList();

            var rows = candidates
                .Where(r => NormalizePlaceName(r.City) == cityKey)
                .ToList();

            var areas = rows
                .Select(x =>
                {
                    if (!string.IsNullOrWhiteSpace(x.Area))
                        return x.Area.Trim();

                    return ExtractAreaFromAddress(x.address);
                })
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            return areas;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var vm = new RestaurantFilterViewModel();

            ViewBag.AvailableCities = GetAvailableCities();

            vm.Results = db.Restaurants
                .Where(r => r.is_approved == true)
                .OrderByDescending(r => r.created_at)
                .Take(9)
                .ToList();

            return View(vm);
        }

        [HttpGet]
        public ActionResult Search(RestaurantFilterViewModel filter)
        {
            if (filter == null)
                filter = new RestaurantFilterViewModel();

            ViewBag.AvailableCities = GetAvailableCities();

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                ViewBag.AvailableAreas = GetAreasByCityInternal(filter.City);
            }
            else
            {
                ViewBag.AvailableAreas = new List<string>();
            }

            var query = db.Restaurants.Where(r => r.is_approved == true);

            if (!string.IsNullOrWhiteSpace(filter.City))
            {
                var variants = GetCityVariants(filter.City);
                if (variants != null && variants.Count > 0)
                {
                    query = query.Where(r => r.City != null && variants.Contains(r.City));
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Area))
            {
                var area = filter.Area.Trim();
                query = query.Where(r =>
                    (r.Area != null && r.Area == area) ||
                    (r.address != null && r.address.Contains(area)));
            }

            if (!string.IsNullOrWhiteSpace(filter.RestaurantType))
            {
                var type = filter.RestaurantType.Trim();
                query = query.Where(r =>
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(type)) ||
                    (r.ServiceTypes != null && r.ServiceTypes.Contains(type)) ||
                    (r.ServiceDescription != null && r.ServiceDescription.Contains(type)));
            }

            if (!string.IsNullOrWhiteSpace(filter.AveragePrice))
            {
                var price = filter.AveragePrice.Trim();
                query = query.Where(r => r.AverageBill == price);
            }

            if (!string.IsNullOrWhiteSpace(filter.MainDish))
            {
                var dish = filter.MainDish.Trim();
                query = query.Where(r => r.SignatureDishes != null && r.SignatureDishes.Contains(dish));
            }

            if (!string.IsNullOrWhiteSpace(filter.SuitableFor))
            {
                var suit = filter.SuitableFor.Trim();
                query = query.Where(r => r.SpaceDescription != null && r.SpaceDescription.Contains(suit));
            }

            if (!string.IsNullOrWhiteSpace(filter.CuisineTag))
            {
                var tag = filter.CuisineTag.Trim();
                query = query.Where(r =>
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(tag)) ||
                    (r.ServiceTypes != null && r.ServiceTypes.Contains(tag)) ||
                    (r.SignatureDishes != null && r.SignatureDishes.Contains(tag)));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchKeyword))
            {
                var kw = filter.SearchKeyword.Trim();
                query = query.Where(r =>
                    (r.name != null && r.name.Contains(kw)) ||
                    (r.address != null && r.address.Contains(kw)) ||
                    (r.CuisineStyle != null && r.CuisineStyle.Contains(kw)) ||
                    (r.SignatureDishes != null && r.SignatureDishes.Contains(kw)));
            }

            filter.Results = query
                .OrderBy(r => r.name)
                .ToList();

            return View("Search", filter);
        }

        [HttpGet]
        public JsonResult GetAreasByCity(string city)
        {
            var areas = GetAreasByCityInternal(city);
            return Json(areas, JsonRequestBehavior.AllowGet);
        }

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

            return View("~/Views/DetailNhaHang/RestaurantDetails.cshtml", vm);
        }

        [HttpGet]
        public JsonResult GetNearbyRestaurants(double lat, double lng)
        {
            var userCoord = new GeoCoordinate(lat, lng);

            var restaurants = db.Restaurants
                .Where(r => r.is_approved == true)
                .ToList()
                .Select(r => new
                {
                    Id = r.restaurant_id,
                    Name = r.name,
                    Address = r.address,
                    Distance = GetDistance(userCoord, r.latitude, r.longitude)
                })
                .Where(r => r.Distance != null && r.Distance < 20)
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
                return userCoord.GetDistanceTo(restaurantCoord) / 1000.0;
            }
            catch
            {
                return null;
            }
        }

        [ChildActionOnly]
        public ActionResult HeaderSearchBar()
        {
            ViewBag.AvailableCities = GetAvailableCities();
            return PartialView("_HeaderSearchBar");
        }
    }
}
