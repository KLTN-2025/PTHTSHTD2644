using System;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System.Data.Entity;

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

        // ========== BƯỚC 1: MÀN HÌNH THANH TOÁN ==========
        [HttpGet]
        public ActionResult Checkout(int bookingId)
        {
            // Nếu DB trống / không có booking -> xử lý an toàn
            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            // Tính tạm số tiền: ví dụ 100.000 / khách
            // Sau này bạn thay bằng giá thật (từ menu, combo, v.v.)
            var soKhach = booking.number_of_guests ?? 1;
            decimal amount = soKhach * 100000m;

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                BookingTime = booking.booking_time ?? DateTime.Now,
                NumberOfGuests = soKhach,
                Amount = amount,
                Status = "Pending"
            };

            return View(vm);   // Views/ThanhToan/Checkout.cshtml
        }

        // ========== BƯỚC 2: XỬ LÝ KHI USER ẤN "HOÀN TẤT THANH TOÁN" ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(PaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var booking = db.Bookings.FirstOrDefault(b => b.booking_id == model.BookingId);
            if (booking == null)
            {
                ModelState.AddModelError("", "Không tìm thấy thông tin đặt bàn.");
                return View(model);
            }

            // Ở đây nếu tích hợp cổng thanh toán (VNPay/MoMo) thì:
            // - Gửi request tới gateway
            // - Nhận callback
            // - Nếu OK thì status = "Đã thanh toán"
            // Tạm thời mock luôn là "Đã thanh toán"
            var payment = new Payments
            {
                booking_id = booking.booking_id,
                amount = model.Amount,
                payment_method = model.PaymentMethod,       // từ form
                status = "Đã thanh toán",                  // mock
                transaction_id = Guid.NewGuid().ToString() // mã giao dịch giả lập
            };

            db.Payments.Add(payment);

            // Nếu bảng Bookings có cột status thì cập nhật
            // (nếu không có cột này thì bỏ dòng này đi)
            try
            {
                booking.status = "Đã thanh toán";
            }
            catch
            {
                // nếu không có trường status thì ignore, không crash
            }

            db.SaveChanges();

            return RedirectToAction("HoanTat", new { bookingId = booking.booking_id });
        }

        // ========== BƯỚC 3: TRANG HOÀN TẤT ==========
        [HttpGet]
        public ActionResult HoanTat(int bookingId)
        {
            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Payments)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var payment = booking.Payments
                                 .OrderByDescending(p => p.payment_id)
                                 .FirstOrDefault();

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                BookingTime = booking.booking_time ?? DateTime.Now,
                NumberOfGuests = booking.number_of_guests ?? 0,
                Amount = payment != null ? payment.amount : 0,
                PaymentMethod = payment != null ? payment.payment_method : null,
                Status = payment != null ? payment.status : "Chưa thanh toán"
            };

            return View(vm);   // Views/ThanhToan/HoanTat.cshtml
        }
    }
}
    