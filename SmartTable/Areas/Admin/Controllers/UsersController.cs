using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin]
    public class UsersController : Controller
    {
        private Entities db = new Entities();

        public ActionResult Index()
        {
            var users = db.Users.ToList();
            ViewBag.Title = "Quản lý Người dùng";
            return View(users);
        }

        [HttpGet]
        public ActionResult ChangePassword(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("Index");
            }

            var vm = new AdminChangePasswordViewModel
            {
                UserId = user.user_id,
                Email = user.email,
                FullName = user.full_name,
                Role = user.role
            };

            return View(vm);
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return null;
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(AdminChangePasswordViewModel model)
        {
            var user = db.Users.Find(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản để đổi mật khẩu.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError("NewPassword", "Vui lòng nhập mật khẩu mới.");

            if (model.NewPassword != model.ConfirmPassword)
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không trùng khớp.");

            if (!ModelState.IsValid)
            {
                model.Email = user.email;
                model.FullName = user.full_name;
                model.Role = user.role;
                return View(model);
            }

            user.password_hash = HashPassword(model.NewPassword);

            db.Entry(user).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã cập nhật mật khẩu cho tài khoản {user.email}.";
            return RedirectToAction("Index");
        }

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
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
