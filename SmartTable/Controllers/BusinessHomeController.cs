using SmartTable.Filters; // Dùng cho [AuthorizeUser]
using SmartTable.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser] // Đảm bảo người dùng phải đăng nhập
    public class BusinessHomeController : Controller
    {
        private Entities db = new Entities();

        // [GET] /BusinessHome/Index (Trang Dashboard chính)
        public ActionResult Index()
        {
            // Kiểm tra vai trò: chỉ cho phép 'business'
            if (Session["role"] == null || Session["role"].ToString() != "business")
            {
                return RedirectToAction("Index", "Home");
            }

            var userId = (int)Session["user_id"];
            var restaurant = db.Restaurants.FirstOrDefault(r => r.user_id == userId);

            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Tài khoản của bạn chưa được liên kết với nhà hàng nào.";
            }

            return View(restaurant);
        }

        // --- HÀM PROFILE (ĐÃ DI CHUYỂN VÀO ĐÚNG VỊ TRÍ) ---
        [AuthorizeUser]
        public ActionResult Profile()
        {
            // Kiểm tra vai trò: chỉ cho phép 'business'
            if (Session["role"] == null || Session["role"].ToString() != "business")
            {
                return RedirectToAction("Index", "Home");
            }

            var userId = (int)Session["user_id"];
            var restaurant = db.Restaurants.FirstOrDefault(r => r.user_id == userId);

            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Chưa có nhà hàng nào liên kết với tài khoản này.";
            }
            return View(restaurant); // Trả về View Profile, kèm theo model Nhà hàng
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    
    // (Thêm vào bên trong class BusinessHomeController)

// --- CHỈNH SỬA THÔNG TIN NHÀ HÀNG (GET) ---
[HttpGet]
        [AuthorizeUser]
        public ActionResult EditRestaurant(int id)
        {
            var restaurant = db.Restaurants.Find(id);
            var userId = (int)Session["user_id"];

            // Security check: Đảm bảo user đang đăng nhập là chủ sở hữu nhà hàng này
            if (restaurant == null || restaurant.user_id != userId)
            {
                return HttpNotFound();
            }

            return View(restaurant); // Gửi model nhà hàng đến View
        }

        // --- CHỈNH SỬA THÔNG TIN NHÀ HÀNG (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeUser]
        public ActionResult EditRestaurant(Restaurants model)
        {
            var userId = (int)Session["user_id"];

            if (ModelState.IsValid)
            {
                // 1. Tìm bản ghi gốc (cần có System.Data.Entity)
                var originalRestaurant = db.Restaurants.Find(model.restaurant_id);

                // Security check
                if (originalRestaurant == null || originalRestaurant.user_id != userId)
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa nhà hàng này.";
                    return RedirectToAction("Profile");
                }

                // 2. Cập nhật các trường (Chỉ cập nhật những trường cần thay đổi)
                originalRestaurant.name = model.name;
                originalRestaurant.address = model.address;
                originalRestaurant.description = model.description;
                originalRestaurant.opening_hours = model.opening_hours;
                originalRestaurant.max_tables = model.max_tables;
                originalRestaurant.Image = model.Image; // Cập nhật URL ảnh

                db.Entry(originalRestaurant).State = EntityState.Modified; // Đánh dấu là đã thay đổi
                db.SaveChanges();

                TempData["SuccessMessage"] = "Cập nhật thông tin nhà hàng thành công!";
                return RedirectToAction("Profile"); // Quay lại trang hồ sơ (Profile)
            }

            // Nếu Model State không hợp lệ (lỗi validation)
            return View(model);
        }
    }
}