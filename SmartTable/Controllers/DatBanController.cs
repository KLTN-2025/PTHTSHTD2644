using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;                    
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Controllers
{
    public class DatBanController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ================== GET: /DatBan/DatBan/{id} ==================
        // Hiển thị màn hình đặt bàn cho 1 nhà hàng
        [HttpGet]
        public ActionResult DatBan(int id)
        {
            var restaurant = db.Restaurants.Find(id);
            if (restaurant == null || restaurant.is_approved != true)
            {
                TempData["ErrorMessage"] = "Nhà hàng không tồn tại hoặc chưa được duyệt.";
                return RedirectToAction("Index", "Home");
            }

            // Có thể nhận sẵn ngày / số khách từ query nếu muốn
            ViewBag.PreSelectedDate = null;
            ViewBag.PreSelectedPeople = null;

            // View: Views/DatBan/DatBan.cshtml (model = Restaurants)
            return View(restaurant);
        }

        // ================== POST: /DatBan/LuuDatBan ==================
        // Nhận dữ liệu form, lưu Booking, sau đó chuyển sang ThanhToan/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LuuDatBan(
            int restaurant_id,
            string booking_time,        // yyyy-MM-dd từ input date
            string booking_time_hour,   // hh:mm từ input hidden
            int number_of_guests,
            string full_name,
            string phone,
            string special_request
        )
        {
            // 1. Kiểm tra nhà hàng
            var restaurant = db.Restaurants.Find(restaurant_id);
            if (restaurant == null || restaurant.is_approved != true)
            {
                TempData["ErrorMessage"] = "Nhà hàng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            // 2. GHÉP NGÀY + GIỜ THÀNH DateTime

            // Ngày
            DateTime datePart;
            if (!DateTime.TryParse(booking_time, out datePart))
            {
                // Nếu lỗi parse -> dùng ngày hôm nay
                datePart = DateTime.Now.Date;
            }

            // Giờ
            TimeSpan timePart;
            if (!TimeSpan.TryParse(booking_time_hour, out timePart))
            {
                // Nếu lỗi / chưa chọn giờ -> mặc định 19:00
                timePart = new TimeSpan(19, 0, 0);
            }

            var bookingDateTime = datePart.Date + timePart;

            // 3. LẤY USER ĐANG ĐĂNG NHẬP (NẾU CÓ)
            var currentUser = Session["user"] as Users;
            int? userId = currentUser != null ? (int?)currentUser.user_id : null;

            // 4. TẠO BẢN GHI BOOKING
            var booking = new Bookings
            {
                user_id = userId,
                restaurant_id = restaurant_id,
                booking_time = bookingDateTime,
                number_of_guests = number_of_guests,
                status = "Đã đặt",   // tuỳ bạn muốn quy ước
                special_request = string.IsNullOrWhiteSpace(special_request)
                    ? null
                    : special_request.Trim()
            };

            db.Bookings.Add(booking);
            db.SaveChanges(); // sau dòng này booking.booking_id đã có

            // 5. SAU KHI LƯU -> CHUYỂN QUA MÀN HÌNH THANH TOÁN
            return RedirectToAction(
                "Checkout",
                "ThanhToan",
                new { bookingId = booking.booking_id }
            );
        }

        // ================== LỊCH SỬ ĐẶT BÀN CỦA NGƯỜI DÙNG ==================
        [HttpGet]
        public ActionResult DanhSachBanDaDat()
        {
            // Lấy user từ session
            var user = Session["user"] as Users;
            if (user == null)
            {
                // Chưa login -> đẩy về trang login
                return RedirectToAction("Login", "Account");
            }

            int userId = user.user_id;

            // Lấy danh sách booking của user
            var bookings = db.Bookings
                             .Include(b => b.Restaurants)   // dùng được vì đã using System.Data.Entity
                             .Include(b => b.Payments)
                             .Where(b => b.user_id == userId)
                             .OrderByDescending(b => b.booking_time)
                             .ToList();

            var list = new List<BookingHistoryItemViewModel>();

            foreach (var b in bookings)
            {
                // Payment mới nhất (nếu có)
                var lastPayment = b.Payments
                                   .OrderByDescending(p => p.payment_id)
                                   .FirstOrDefault();

                decimal? deposit = null;
                string paymentStatus = "Chưa thanh toán";

                if (lastPayment != null)
                {
                    deposit = lastPayment.amount;
                    paymentStatus = string.IsNullOrEmpty(lastPayment.status)
                        ? "Chưa cập nhật"
                        : lastPayment.status;
                }

                var vm = new BookingHistoryItemViewModel
                {
                    BookingId = b.booking_id,
                    RestaurantName = b.Restaurants != null ? b.Restaurants.name : "Nhà hàng",
                    RestaurantAddress = b.Restaurants != null ? b.Restaurants.address : "",
                    RestaurantImage = b.Restaurants != null && !string.IsNullOrEmpty(b.Restaurants.Image)
                                           ? b.Restaurants.Image
                                           : "https://via.placeholder.com/120",

                    BookingTime = b.booking_time,
                    NumberOfGuests = b.number_of_guests,
                    SpecialRequest = b.special_request,

                    BookingStatus = string.IsNullOrEmpty(b.status) ? "Chờ xác nhận" : b.status,
                    DepositAmount = deposit,
                    PaymentStatus = paymentStatus
                };

                list.Add(vm);
            }

            // View: Views/DatBan/DanhSachBanDaDat.cshtml
            return View(list);
        }
    }
}
