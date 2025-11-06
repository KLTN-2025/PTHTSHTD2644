using SmartTable.Filters;
using SmartTable.Models;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Areas.Admin.Controllers
{
    [AuthorizeAdmin] // Chỉ cho phép Admin truy cập
    public class RoleController : Controller
    {
        private Entities db = new Entities();

        // GET: Admin/Role
        public ActionResult Index()
        {
            // Trả về danh sách tất cả người dùng để Admin có thể xem và sửa Role
            var users = db.Users.ToList();
            ViewBag.Title = "Quản lý Vai trò và Tài khoản";
            return View(users);
        }

        // POST: Admin/Role/UpdateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateRole(int userId, string newRole)
        {
            var user = db.Users.Find(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("Index");
            }

            // Logic cập nhật Role
            user.role = newRole;
            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã cập nhật vai trò của {user.full_name} thành {newRole}.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}