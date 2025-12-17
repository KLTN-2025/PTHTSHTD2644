using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Controllers
{
    public class NhaHangUyTinController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult Index()
        {
            var minReviews = 1;
            var minRating = 3.6m;

            ViewBag.MinReviews = minReviews;
            ViewBag.MinRating = minRating;

            var restaurants = db.Restaurants
                .Where(r => r.is_approved == true && r.is_locked == false)
                .Select(r => new
                {
                    r.restaurant_id,
                    r.name,
                    r.address,
                    r.Image,
                    r.City,
                    r.Area
                })
                .ToList();

            var ids = restaurants.Select(x => x.restaurant_id).ToList();

            var approvedReviews = db.Reviews
                .Where(rv => rv.restaurant_id.HasValue
                             && ids.Contains(rv.restaurant_id.Value)
                             && (rv.status == "approved" || rv.status == null))
                .Select(rv => new
                {
                    restaurant_id = rv.restaurant_id.Value,
                    rating = (byte?)rv.rating
                })
                .ToList();

            var list = new List<TrustedRestaurantItemViewModel>();

            for (int i = 0; i < restaurants.Count; i++)
            {
                var r = restaurants[i];

                var rs = approvedReviews.Where(x => x.restaurant_id == r.restaurant_id).ToList();
                var count = rs.Count;

                decimal avg = 0m;
                if (count > 0)
                {
                    decimal sum = 0m;
                    for (int k = 0; k < rs.Count; k++)
                    {
                        sum += (decimal)(rs[k].rating.HasValue ? rs[k].rating.Value : (byte)0);
                    }
                    avg = sum / count;
                }

                if (count >= minReviews && avg >= minRating)
                {
                    list.Add(new TrustedRestaurantItemViewModel
                    {
                        RestaurantId = r.restaurant_id,
                        Name = r.name,
                        Address = r.address,
                        Image = r.Image,
                        City = r.City,
                        Area = r.Area,
                        ReviewCount = count,
                        AvgRating = avg
                    });
                }
            }

            list = list
                .OrderByDescending(x => x.AvgRating)
                .ThenByDescending(x => x.ReviewCount)
                .ToList();

            return View(list);
        }

        [ChildActionOnly]
        public ActionResult _TopUyTin(int take)
        {
            if (take <= 0) take = 6;

            var minReviews = 1;
            var minRating = 3.6m;

            var restaurants = db.Restaurants
                .Where(r => r.is_approved == true && r.is_locked == false)
                .Select(r => new
                {
                    r.restaurant_id,
                    r.name,
                    r.address,
                    r.Image
                })
                .ToList();

            var ids = restaurants.Select(x => x.restaurant_id).ToList();

            var approvedReviews = db.Reviews
                .Where(rv => rv.restaurant_id.HasValue
                             && ids.Contains(rv.restaurant_id.Value)
                             && (rv.status == "approved" || rv.status == null))
                .Select(rv => new
                {
                    restaurant_id = rv.restaurant_id.Value,
                    rating = (byte?)rv.rating
                })
                .ToList();

            var list = new List<TrustedRestaurantItemViewModel>();

            for (int i = 0; i < restaurants.Count; i++)
            {
                var r = restaurants[i];

                var rs = approvedReviews.Where(x => x.restaurant_id == r.restaurant_id).ToList();
                var count = rs.Count;

                decimal avg = 0m;
                if (count > 0)
                {
                    decimal sum = 0m;
                    for (int k = 0; k < rs.Count; k++)
                    {
                        sum += (decimal)(rs[k].rating.HasValue ? rs[k].rating.Value : (byte)0);
                    }
                    avg = sum / count;
                }

                if (count >= minReviews && avg >= minRating)
                {
                    list.Add(new TrustedRestaurantItemViewModel
                    {
                        RestaurantId = r.restaurant_id,
                        Name = r.name,
                        Address = r.address,
                        Image = r.Image,
                        ReviewCount = count,
                        AvgRating = avg
                    });
                }
            }

            list = list
                .OrderByDescending(x => x.AvgRating)
                .ThenByDescending(x => x.ReviewCount)
                .Take(take)
                .ToList();

            return PartialView("_TopUyTin", list);
        }
    }
}
