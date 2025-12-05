using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SmartTable.Filters;
using SmartTable.Models;

namespace SmartTable.Controllers
{
    [AuthorizeUser] 
    public class MenuController : Controller
    {
        private readonly Entities db = new Entities();

        private int? GetCurrentUserId()
        {
            if (Session["user_id"] == null) return null;
            return Convert.ToInt32(Session["user_id"]);
        }

        private bool IsBusiness()
        {
            return Session["role"] != null &&
                   Session["role"].ToString()
                          .Equals("business", StringComparison.OrdinalIgnoreCase);
        }

        private Restaurants GetCurrentRestaurant()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return null;

            return db.Restaurants.FirstOrDefault(r => r.user_id == userId.Value);
        }

        // =============DANH SÁCH MÓN =============
        public ActionResult Index()
        {
            if (!IsBusiness())
            {
                TempData["Error"] = "Bạn không có quyền truy cập trang này.";
                return RedirectToAction("Index", "Home");
            }

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["Error"] = "Tài khoản của bạn chưa gắn với nhà hàng nào.";
                return RedirectToAction("Index", "BusinessHome");
            }

            var menuItems = db.MenuItems
                              .Where(m => m.restaurant_id == restaurant.restaurant_id)
                              .OrderBy(m => m.category)
                              .ThenBy(m => m.name)
                              .ToList();

            ViewBag.RestaurantId = restaurant.restaurant_id;
            return View(menuItems); 
        }

        // ============= TẠO MÓN MỚI =============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MenuItems menuItem, HttpPostedFileBase Image)
        {
            if (!IsBusiness())
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["Error"] = "Không tìm thấy nhà hàng của bạn.";
                return RedirectToAction("Index");
            }

            menuItem.restaurant_id = restaurant.restaurant_id;

            menuItem.is_available =
                Request.Form["is_available"] == "true" ||
                Request.Form["is_available"] == "on";

            if (Image != null && Image.ContentLength > 0)
            {
                var validTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!validTypes.Contains(Image.ContentType))
                {
                    TempData["Error"] = "Vui lòng chọn file ảnh (JPG, PNG, GIF).";
                    return RedirectToAction("Index");
                }

                if (Image.ContentLength > 2 * 1024 * 1024)
                {
                    TempData["Error"] = "Kích thước ảnh không được vượt quá 2MB.";
                    return RedirectToAction("Index");
                }

                var uploadDir = Server.MapPath("~/Content/Uploads/MenuImages/");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var fileName = Guid.NewGuid() + Path.GetExtension(Image.FileName);
                var path = Path.Combine(uploadDir, fileName);
                Image.SaveAs(path);

                menuItem.Image = "/Content/Uploads/MenuImages/" + fileName;
            }

            db.MenuItems.Add(menuItem);
            db.SaveChanges();

            TempData["Success"] = "Thêm món ăn thành công.";
            return RedirectToAction("Index");
        }

        // ============= SỬA MÓN =============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MenuItems menuItem, HttpPostedFileBase Image)
        {
            if (!IsBusiness())
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["Error"] = "Không tìm thấy nhà hàng của bạn.";
                return RedirectToAction("Index");
            }

            var existingItem = db.MenuItems.Find(menuItem.menu_item_id);
            if (existingItem == null || existingItem.restaurant_id != restaurant.restaurant_id)
            {
                TempData["Error"] = "Bạn không có quyền chỉnh sửa món ăn này.";
                return RedirectToAction("Index");
            }

            existingItem.name = menuItem.name;
            existingItem.category = menuItem.category;
            existingItem.price = menuItem.price;
            existingItem.description = menuItem.description;

            existingItem.is_available =
                Request.Form["is_available"] == "true" ||
                Request.Form["is_available"] == "on";

            if (Image != null && Image.ContentLength > 0)
            {
                var validTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!validTypes.Contains(Image.ContentType))
                {
                    TempData["Error"] = "Vui lòng chọn file ảnh (JPG, PNG, GIF).";
                    return RedirectToAction("Index");
                }

                if (Image.ContentLength > 2 * 1024 * 1024)
                {
                    TempData["Error"] = "Kích thước ảnh không được vượt quá 2MB.";
                    return RedirectToAction("Index");
                }

                var uploadDir = Server.MapPath("~/Content/Uploads/MenuImages/");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var fileName = Guid.NewGuid() + Path.GetExtension(Image.FileName);
                var path = Path.Combine(uploadDir, fileName);
                Image.SaveAs(path);

                existingItem.Image = "/Content/Uploads/MenuImages/" + fileName;
            }

            db.SaveChanges();
            TempData["Success"] = "Cập nhật món ăn thành công.";
            return RedirectToAction("Index");
        }

        // ============= BẬT / TẮT ĐANG BÁN =============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleAvailable(int id)
        {
            if (!IsBusiness())
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["Error"] = "Không tìm thấy nhà hàng của bạn.";
                return RedirectToAction("Index");
            }

            var menuItem = db.MenuItems
                             .FirstOrDefault(m => m.menu_item_id == id &&
                                                  m.restaurant_id == restaurant.restaurant_id);

            if (menuItem == null)
            {
                TempData["Error"] = "Không tìm thấy món ăn hoặc bạn không có quyền.";
                return RedirectToAction("Index");
            }

            var newStatus = !menuItem.is_available.GetValueOrDefault();
            menuItem.is_available = newStatus;

            db.SaveChanges();

            TempData["Success"] = newStatus
                ? $"Đã bật bán cho món \"{menuItem.name}\"."
                : $"Đã tạm ngưng món \"{menuItem.name}\".";

            return RedirectToAction("Index");
        }

        // ============= XÓA MÓN =============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsBusiness())
            {
                TempData["Error"] = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["Error"] = "Không tìm thấy nhà hàng của bạn.";
                return RedirectToAction("Index");
            }

            var menuItem = db.MenuItems
                             .FirstOrDefault(m => m.menu_item_id == id &&
                                                  m.restaurant_id == restaurant.restaurant_id);

            if (menuItem == null)
            {
                TempData["Error"] = "Bạn không có quyền xóa món ăn này.";
                return RedirectToAction("Index");
            }

            db.MenuItems.Remove(menuItem);
            db.SaveChanges();

            TempData["Success"] = "Xóa món ăn thành công.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
