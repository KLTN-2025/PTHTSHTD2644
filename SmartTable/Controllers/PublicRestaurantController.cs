using System.Web.Mvc;
using SmartTable.Models;
using System.Linq;
using System.Collections.Generic;
using System.Data.Entity;
using System.Net;
using System;
using System.Device.Location; 
using System.Globalization;

public class PublicRestaurantController : Controller 
{
    private Entities db = new Entities(); 

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

    public ActionResult Nearby()
    {
        return View(); 
    }

    public ActionResult Details(int? id)
    {
        if (id == null)
        {
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        }
        Restaurants restaurant = db.Restaurants
                                    .Include(r => r.Reviews)
                                    .Include(r => r.MenuItems)
                                    .Include(r => r.Users)
                                    .FirstOrDefault(r => r.restaurant_id == id && r.is_approved == true);

        if (restaurant == null)
        {
            return HttpNotFound();
        }

        return View("~/Views/DetailNhaHang/RestaurantDetails.cshtml");
    }


    [HttpGet]
    public JsonResult GetNearbyMapData(double lat, double lng, double radiusKm = 5)
    {
        var allRestaurants = db.Restaurants
            .Where(r => r.latitude != null && r.longitude != null && r.is_approved == true)
            .AsNoTracking() 
            .ToList();

        var nearbyRestaurants = allRestaurants
            .Select(r => new
            {
                r.restaurant_id,
                r.name,
                r.address,
                r.Image,
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