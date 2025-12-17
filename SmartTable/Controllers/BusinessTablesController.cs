using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using SmartTable.Services;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class BusinessTablesController : Controller
    {
        private readonly Entities db = new Entities();
        private readonly ITableManagementService _tableService;

        public BusinessTablesController()
        {
            _tableService = new TableManagementService(db);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ===== AUTH HELPERS =====
        private int? GetCurrentUserId()
        {
            if (Session["user_id"] == null) return null;

            int id;
            return int.TryParse(Session["user_id"].ToString(), out id) ? (int?)id : null;
        }

        private bool IsBusiness()
        {
            return Session["role"] != null
                && string.Equals(Session["role"].ToString(), "business", StringComparison.OrdinalIgnoreCase);
        }

        private Restaurants GetCurrentRestaurant()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return null;

            return db.Restaurants
                .Include(r => r.Tables)
                .FirstOrDefault(r => r.user_id == userId.Value);
        }

        // ===== LOGGING (chống 500 mù) =====
        private void LogError(string action, Exception ex, object payload = null)
        {
            try
            {
                var dir = Server.MapPath("~/App_Data/logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var path = Path.Combine(dir, "business_tables_errors.log");
                var msg =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {action}\n" +
                    $"Payload: {(payload == null ? "null" : payload.ToString())}\n" +
                    ex.ToString() + "\n-------------------------\n";

                System.IO.File.AppendAllText(path, msg);
            }
            catch
            {
                // không để log làm crash tiếp
            }
        }

        // ===== DANH SÁCH BÀN =====
        [HttpGet]
        public ActionResult Index()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                ViewBag.ErrorMessage = "Tài khoản chưa liên kết với nhà hàng nào.";
                return View(new RestaurantTableStatusViewModel());
            }

            var model = _tableService.GetRestaurantTableStatus(restaurant.restaurant_id);
            return View(model);
        }

        // ===== CHECK-IN KHÁCH =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckIn(int tableId, int bookingId)
        {
            if (!IsBusiness())
                return Json(new { success = false, message = "Không có quyền." });

            if (tableId <= 0 || bookingId <= 0)
                return Json(new { success = false, message = "Thiếu mã bàn hoặc mã booking." });

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
                return Json(new { success = false, message = "Tài khoản chưa liên kết nhà hàng." });

            try
            {
                var table = db.Tables.FirstOrDefault(t => t.table_id == tableId && t.restaurant_id == restaurant.restaurant_id);
                if (table == null)
                    return Json(new { success = false, message = "Bàn không thuộc nhà hàng của bạn." });

                var booking = db.Bookings.FirstOrDefault(b =>
                    b.booking_id == bookingId &&
                    b.restaurant_id == restaurant.restaurant_id &&
                    b.table_id == tableId
                );

                if (booking == null)
                    return Json(new { success = false, message = "Booking không thuộc bàn này hoặc không tồn tại." });

                bool result = _tableService.CheckInTable(tableId, bookingId);

                return Json(new
                {
                    success = result,
                    message = result ? "✅ Check-in thành công." : "❌ Check-in thất bại (status booking không hợp lệ hoặc dữ liệu không khớp)."
                });
            }
            catch (Exception ex)
            {
                LogError("CheckIn", ex, new { tableId, bookingId, restaurantId = restaurant.restaurant_id });
                return Json(new { success = false, message = "❌ Server lỗi khi check-in. Vui lòng thử lại hoặc xem log." });
            }
        }

        // ===== CHECK-OUT KHÁCH =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOut(int tableId)
        {
            if (!IsBusiness())
                return Json(new { success = false, message = "Không có quyền." });

            if (tableId <= 0)
                return Json(new { success = false, message = "Thiếu mã bàn." });

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
                return Json(new { success = false, message = "Tài khoản chưa liên kết nhà hàng." });

            try
            {
                var table = db.Tables.FirstOrDefault(t => t.table_id == tableId && t.restaurant_id == restaurant.restaurant_id);
                if (table == null)
                    return Json(new { success = false, message = "Bàn không thuộc nhà hàng của bạn." });

                bool result = _tableService.CheckOutTable(tableId);

                return Json(new
                {
                    success = result,
                    message = result ? "✅ Check-out thành công." : "❌ Check-out thất bại (không có booking đang checked-in)."
                });
            }
            catch (Exception ex)
            {
                LogError("CheckOut", ex, new { tableId, restaurantId = restaurant.restaurant_id });
                return Json(new { success = false, message = "❌ Server lỗi khi check-out. Vui lòng thử lại hoặc xem log." });
            }
        }

        // ===== TẠO BÀN =====
        [HttpGet]
        public ActionResult Create()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            return View(new Tables { restaurant_id = restaurant.restaurant_id, is_available = true });
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
                TempData["ErrorMessage"] = "Bạn chưa có nhà hàng.";
                return RedirectToAction("Index");
            }

            model.restaurant_id = restaurant.restaurant_id;

            if (string.IsNullOrWhiteSpace(model.table_number))
                ModelState.AddModelError("table_number", "Vui lòng nhập số bàn.");

            if (!model.capacity.HasValue || model.capacity.Value <= 0)
                ModelState.AddModelError("capacity", "Sức chứa phải lớn hơn 0.");

            if (db.Tables.Any(t => t.restaurant_id == restaurant.restaurant_id && t.table_number == model.table_number))
                ModelState.AddModelError("table_number", "Số bàn này đã tồn tại.");

            if (!ModelState.IsValid)
                return View(model);

            model.is_available = true;
            db.Tables.Add(model);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Thêm bàn thành công.";
            return RedirectToAction("Index");
        }

        // ===== CHỈNH SỬA BÀN =====
        [HttpGet]
        public ActionResult Edit(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null) return HttpNotFound();

            var table = db.Tables.FirstOrDefault(t => t.table_id == id && t.restaurant_id == restaurant.restaurant_id);
            if (table == null) return HttpNotFound();

            return View(table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Tables model)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null) return HttpNotFound();

            var table = db.Tables.FirstOrDefault(t => t.table_id == model.table_id && t.restaurant_id == restaurant.restaurant_id);
            if (table == null) return HttpNotFound();

            table.table_number = model.table_number;
            table.capacity = model.capacity;
            table.type = model.type;
            table.is_available = model.is_available;

            db.SaveChanges();

            TempData["SuccessMessage"] = "Cập nhật bàn thành công.";
            return RedirectToAction("Index");
        }

        // ===== XÓA BÀN =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var restaurant = GetCurrentRestaurant();
            if (restaurant == null)
                return RedirectToAction("Index");

            var table = db.Tables.FirstOrDefault(t => t.table_id == id && t.restaurant_id == restaurant.restaurant_id);
            if (table == null)
                return RedirectToAction("Index");

            db.Tables.Remove(table);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Xóa bàn thành công.";
            return RedirectToAction("Index");
        }
    }
}
