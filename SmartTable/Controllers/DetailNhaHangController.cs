using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;

namespace SmartTable.Controllers
{
    public class DetailNhaHangController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult RestaurantDetails(int id)
        {
            var restaurant = db.Restaurants
                               .Include(r => r.RestaurantImages)
                               .FirstOrDefault(r => r.restaurant_id == id && r.is_approved == true);

            if (restaurant == null)
                return HttpNotFound();

            var vm = new SmartTableDetailViewModel
            {
                Restaurant = restaurant,
                MenuItems = db.MenuItems
                              .Where(m => m.restaurant_id == id && (m.is_available ?? true))
                              .ToList(),
                Reviews = db.Reviews
                            .Where(r => r.restaurant_id == id)
                            .Include(r => r.Users)
                            .ToList()
            };

            return View(vm);
        }

        // POST: /DetailNhaHang/PostReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PostReview(int restaurant_id, byte rating, string comment)
        {
            var user = Session["user"] as Users;
            if (user == null)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để đánh giá.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = db.Restaurants.Find(restaurant_id);
            if (restaurant == null || restaurant.is_approved != true)
            {
                TempData["ErrorMessage"] = "Nhà hàng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            if (rating < 1) rating = 1;
            if (rating > 5) rating = 5;

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập nội dung đánh giá.";
                return RedirectToAction("RestaurantDetails", new { id = restaurant_id });
            }

            var review = new Reviews
            {
                user_id = user.user_id,
                restaurant_id = restaurant_id,
                rating = rating,
                comment = comment.Trim(),
                created_at = DateTime.Now
            };

            db.Reviews.Add(review);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá!";
            return RedirectToAction("RestaurantDetails", new { id = restaurant_id });
        }
    }
}
