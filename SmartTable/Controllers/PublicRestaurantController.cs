using System.Web.Mvc;
using SmartTable.Models;
using System.Linq;
using System.Collections.Generic;
using System.Data.Entity;
using System.Net;
using System;
using System.Device.Location; 
using System.Globalization;

// Controller này phục vụ các chức năng CÔNG CỘNG
public class PublicRestaurantController : Controller 
{
    private Entities db = new Entities(); 

    // Hàm tiện ích tính khoảng cách giữa 2 tọa độ (Km)
    private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
    {
        var coord1 = new GeoCoordinate(lat1, lng1);
        var coord2 = new GeoCoordinate(lat2, lng2);
        return coord1.GetDistanceTo(coord2) / 1000;
    }

    // Hàm hỗ trợ chuyển đổi từ độ sang radian 
    private double ToRadians(double degree)
    {
        return degree * Math.PI / 180;
    }

    // GET: /PublicRestaurant/Nearby (Hiển thị trang Map và Danh sách nhà hàng gần đó)
    public ActionResult Nearby()
    {
        return View(); // View sẽ nằm ở Views/Restaurant/Nearby.cshtml
    }

    // GET: /Restaurant/Details/5 (Hiển thị trang chi tiết nhà hàng công cộng)
    public ActionResult Details(int? id)
    {
        if (id == null)
        {
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }
        // Chỉ lấy nhà hàng đã được duyệt
        Restaurants restaurant = db.Restaurants
                                    .Include(r => r.Reviews)
                                    .Include(r => r.MenuItems)
                                    .Include(r => r.Users)
                                    .FirstOrDefault(r => r.restaurant_id == id && r.is_approved == true);

        if (restaurant == null)
        {
            return HttpNotFound();
        }

        return View(restaurant);
    }


    // GET: /PublicRestaurant/GetNearbyMapData (Action AJAX lấy dữ liệu Nhà hàng gần đó)
    [HttpGet]
    public JsonResult GetNearbyMapData(double lat, double lng, double radiusKm = 5)
    {
        // Lọc: Chỉ lấy nhà hàng ĐÃ ĐƯỢC DUYỆT (is_approved == true)
        var allRestaurants = db.Restaurants
            .Where(r => r.latitude != null && r.longitude != null && r.is_approved == true)
            .AsNoTracking() // Dùng AsNoTracking để tối ưu tốc độ truy vấn
            .ToList();

        var nearbyRestaurants = allRestaurants
            .Select(r => new
            {
                r.restaurant_id,
                r.name,
                r.address,
                r.Image,
                // Fix lỗi: Đảm bảo sử dụng giá trị double? của Model
                latitude = r.latitude,
                longitude = r.longitude,
                distanceKm = CalculateDistance(lat, lng, r.latitude.Value, r.longitude.Value)
            })
            .Where(r => r.distanceKm <= radiusKm)
            .OrderBy(r => r.distanceKm)
            .ToList();

        return Json(nearbyRestaurants, JsonRequestBehavior.AllowGet);
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