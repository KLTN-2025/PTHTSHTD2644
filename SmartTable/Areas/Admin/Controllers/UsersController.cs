using SmartTable.Filters;
using SmartTable.Models;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] 
    public class UsersController : Controller
    {
        private Entities db = new Entities();

        // GET: Admin/Users
        public ActionResult Index()
        {
            var users = db.Users.ToList();
            ViewBag.Title = "Quản lý Người dùng";
            return View(users);
        }

        // POST: Admin/Users/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản để xóa.";
                return RedirectToAction("Index");
            }

            var userBookings = db.Bookings.Where(b => b.user_id == id);
            db.Bookings.RemoveRange(userBookings);

            var userReviews = db.Reviews.Where(r => r.user_id == id);
            db.Reviews.RemoveRange(userReviews);

            var associatedRestaurant = db.Restaurants.FirstOrDefault(r => r.user_id == id);
            if (associatedRestaurant != null)
            {
                associatedRestaurant.user_id = null;
            }

            db.Users.Remove(user);
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa tài khoản {user.email} thành công.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}