using SmartTable.Filters;
using SmartTable.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class BusinessTablesController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ==== Helper: Lấy user_id hiện tại ====
        private int? GetCurrentUserId()
        {
            if (Session["user_id"] == null) return null;
            int id;
            if (int.TryParse(Session["user_id"].ToString(), out id))
                return id;
            return null;
        }

        // ==== Helper: Kiểm tra role business ====
        private bool IsBusiness()
        {
            return Session["role"] != null && Session["role"].ToString() == "business";
        }

        // ==== Helper: lấy nhà hàng của business hiện tại ====
        private Restaurants GetCurrentRestaurant()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return null;

            var restaurant = db.Restaurants
                               .Include(r => r.Tables)
                               .FirstOrDefault(r => r.user_id == userId.Value);

            return restaurant;
        }

        // ================== DANH SÁCH BÀN ==================
        [HttpGet]
        public ActionResult Index()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Tài khoản của bạn chưa được liên kết với nhà hàng nào. Vui lòng liên hệ Admin.";
                return View(Enumerable.Empty<Tables>());
            }

            var tables = restaurant.Tables
                                   .OrderBy(t => t.table_number)
                                   .ToList();

            ViewBag.RestaurantName = restaurant.name;
            return View(tables);
        }

        // ================== TẠO BÀN MỚI ==================
        [HttpGet]
        public ActionResult Create()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng. Vui lòng liên hệ Admin.";
                return RedirectToAction("Index");
            }

            var model = new Tables
            {
                restaurant_id = restaurant.restaurant_id,
                is_available = true
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Tables model)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng. Vui lòng liên hệ Admin.";
                return RedirectToAction("Index");
            }

            model.restaurant_id = restaurant.restaurant_id;

            if (string.IsNullOrWhiteSpace(model.table_number))
            {
                ModelState.AddModelError("table_number", "Vui lòng nhập số bàn / mã bàn.");
            }

            if (!model.capacity.HasValue || model.capacity.Value <= 0)
            {
                ModelState.AddModelError("capacity", "Sức chứa phải lớn hơn 0.");
            }

            bool existed = db.Tables.Any(t =>
                t.restaurant_id == restaurant.restaurant_id &&
                t.table_number == model.table_number);

            if (existed)
            {
                ModelState.AddModelError("table_number", "Số bàn này đã tồn tại trong nhà hàng.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!model.is_available.HasValue)
                model.is_available = true;

            db.Tables.Add(model);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã thêm bàn mới thành công.";
            return RedirectToAction("Index");
        }

        // ================== CHỈNH SỬA BÀN ==================
        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            var table = db.Tables.FirstOrDefault(t =>
                t.table_id == id &&
                t.restaurant_id == restaurant.restaurant_id);

            if (table == null)
                return HttpNotFound();

            return View(table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tables model)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            var table = db.Tables.FirstOrDefault(t =>
                t.table_id == model.table_id &&
                t.restaurant_id == restaurant.restaurant_id);

            if (table == null)
                return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.table_number))
            {
                ModelState.AddModelError("table_number", "Vui lòng nhập số bàn / mã bàn.");
            }

            if (!model.capacity.HasValue || model.capacity.Value <= 0)
            {
                ModelState.AddModelError("capacity", "Sức chứa phải lớn hơn 0.");
            }

            bool existed = db.Tables.Any(t =>
                t.restaurant_id == restaurant.restaurant_id &&
                t.table_number == model.table_number &&
                t.table_id != model.table_id);

            if (existed)
            {
                ModelState.AddModelError("table_number", "Số bàn này đã tồn tại trong nhà hàng.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Cập nhật
            table.table_number = model.table_number;
            table.capacity = model.capacity;
            table.type = model.type;
            table.is_available = model.is_available;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã cập nhật thông tin bàn.";
            return RedirectToAction("Index");
        }

        // ================== ĐỔI TRẠNG THÁI BÀN ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Toggle(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            var table = db.Tables.FirstOrDefault(t =>
                t.table_id == id &&
                t.restaurant_id == restaurant.restaurant_id);

            if (table == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bàn.";
                return RedirectToAction("Index");
            }

            bool current = table.is_available ?? true;
            table.is_available = !current;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã cập nhật trạng thái bàn.";
            return RedirectToAction("Index");
        }

        // ================== XÓA BÀN ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            var table = db.Tables.FirstOrDefault(t =>
                t.table_id == id &&
                t.restaurant_id == restaurant.restaurant_id);

            if (table == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy bàn.";
                return RedirectToAction("Index");
            }

            db.Tables.Remove(table);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã xóa bàn.";
            return RedirectToAction("Index");
        }
    }
}
