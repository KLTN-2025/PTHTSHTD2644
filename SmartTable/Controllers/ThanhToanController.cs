using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Controllers
{
    public class ThanhToanController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ================== BƯỚC 1: MÀN HÌNH THANH TOÁN (GET) ==================
        [HttpGet]
        public ActionResult Checkout(int bookingId)
        {
            // Nếu bookingId không hợp lệ
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn (bookingId).";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Users)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            // number_of_guests: int (NON-nullable)
            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            // Tạm tính: 50.000 / khách
            decimal amount = soKhach * 50000m;

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                RestaurantAddress = booking.Restaurants != null ? booking.Restaurants.address : null,
                RestaurantImage = booking.Restaurants != null ? booking.Restaurants.Image : null,

                BookingTime = booking.booking_time,       // DateTime non-nullable
                NumberOfGuests = soKhach,
                SpecialRequest = booking.special_request,

                Amount = amount,
                PaymentMethod = "Chuyển khoản",
                Status = "Pending",

                CustomerName = booking.Users != null ? booking.Users.full_name : null,
                TransferContent = $"SMARTTABLE-{booking.booking_id}"
            };

            return View(vm);   // Views/ThanhToan/Checkout.cshtml (model: PaymentViewModel)
        }

        // ================== BƯỚC 2: SUBMIT FORM THANH TOÁN (POST) ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(PaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Trả về lại view với model hiện tại để hiển thị lỗi
                return View(model);
            }

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Users)
                            .FirstOrDefault(b => b.booking_id == model.BookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            // Nếu Amount trên view = 0 thì tính lại cho chắc
            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            var amount = model.Amount > 0 ? model.Amount : soKhach * 50000m;

            // Tạo bản ghi thanh toán
            var payment = new Payments
            {
                booking_id = booking.booking_id,
                amount = amount,
                payment_method = string.IsNullOrEmpty(model.PaymentMethod)
                                    ? "Chuyển khoản"
                                    : model.PaymentMethod,
                status = "Đã thanh toán",
                transaction_id = Guid.NewGuid().ToString()
            };

            db.Payments.Add(payment);

            // Cập nhật trạng thái booking nếu có field status
            try
            {
                booking.status = "Đã thanh toán";
            }
            catch
            {
                // Nếu Bookings không có cột status thì bỏ qua, không crash
            }

            db.SaveChanges();

            // Chuyển sang trang hoàn tất
            return RedirectToAction("HoanTat", new { bookingId = booking.booking_id });
        }

        // ================== API XÁC NHẬN THANH TOÁN (CHO NÚT "TÔI ĐÃ CHUYỂN KHOẢN") ==================
        [HttpPost]
        public ActionResult ConfirmPayment(int bookingId)
        {
            var booking = db.Bookings
                            .Include(b => b.Payments)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy thông tin đặt bàn."
                });
            }

            var payment = booking.Payments
                                 .OrderByDescending(p => p.payment_id)
                                 .FirstOrDefault();

            if (payment != null)
            {
                payment.status = "Đã thanh toán";
            }

            try
            {
                booking.status = "Đã thanh toán";
            }
            catch
            {
            }

            db.SaveChanges();

            return Json(new { success = true });
        }

        // ================== BƯỚC 3: TRANG HOÀN TẤT ==================
        [HttpGet]
        public ActionResult HoanTat(int bookingId)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn (bookingId).";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Payments)
                            .Include(b => b.Users)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var payment = booking.Payments
                                 .OrderByDescending(p => p.payment_id)
                                 .FirstOrDefault();

            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            decimal amount = payment != null ? payment.amount : soKhach * 50000m;

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                RestaurantAddress = booking.Restaurants != null ? booking.Restaurants.address : null,
                RestaurantImage = booking.Restaurants != null ? booking.Restaurants.Image : null,

                BookingTime = booking.booking_time,
                NumberOfGuests = soKhach,
                SpecialRequest = booking.special_request,

                Amount = amount,
                PaymentMethod = payment != null ? payment.payment_method : null,
                Status = payment != null ? payment.status : "Chờ xác nhận",
                CustomerName = booking.Users != null ? booking.Users.full_name : null
            };

            return View(vm); // Views/ThanhToan/HoanTat.cshtml (model: PaymentViewModel)
        }
    }
}
