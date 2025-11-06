using SmartTable.Filters;
using SmartTable.Models;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity; // Cần cho Include

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] // Chỉ Admin được truy cập
    public class UsersController : Controller
    {
        private Entities db = new Entities();

        // GET: Admin/Users
        public ActionResult Index()
        {
            // Trả về danh sách tất cả người dùng
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

            // QUAN TRỌNG: XỬ LÝ DỮ LIỆU LIÊN QUAN TRƯỚC KHI XÓA
            // 1. Xóa các Booking, Reviews, Orders mà User này đã tạo
            var userBookings = db.Bookings.Where(b => b.user_id == id);
            db.Bookings.RemoveRange(userBookings);

            var userReviews = db.Reviews.Where(r => r.user_id == id);
            db.Reviews.RemoveRange(userReviews);

            // 2. Nếu User là CHỦ NHÀ HÀNG (Business), KHÔNG ĐƯỢC XÓA Nhà Hàng của họ.
            // Thay vào đó, set user_id của Nhà hàng đó thành NULL (hoặc một Admin ID)
            var associatedRestaurant = db.Restaurants.FirstOrDefault(r => r.user_id == id);
            if (associatedRestaurant != null)
            {
                // Chỉ set user_id = null, KHÔNG XÓA nhà hàng
                associatedRestaurant.user_id = null;
            }

            // 3. Xóa User chính
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