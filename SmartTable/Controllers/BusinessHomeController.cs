using SmartTable.Filters; // Dùng cho [AuthorizeUser]
using SmartTable.Models;
using System.Linq;
using System.Web.Mvc;
using System;
using System.Collections.Generic;

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
    } // <-- ĐÓNG CLASS
} // <-- ĐÓNG NAMESPACE