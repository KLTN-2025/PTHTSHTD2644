using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System.Globalization;

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

        [HttpGet]
        public ActionResult DatBan(int id, DateTime? ReservationDate, int? NumberOfPeople, string ReservationTime)
        {
            var restaurant = db.Restaurants.Find(id);
            if (restaurant == null || restaurant.is_approved != true)
            {
                TempData["ErrorMessage"] = "Nhà hàng không tồn tại hoặc chưa được duyệt.";
                return RedirectToAction("Index", "Home");
            }

            var date = (ReservationDate ?? DateTime.Today).Date;
            var people = NumberOfPeople ?? 2;

            ViewBag.PreSelectedDate = date;
            ViewBag.PreSelectedPeople = people;
            ViewBag.PreSelectedTime = ReservationTime;

            var menuItems = db.MenuItems
                             .Where(m => m.restaurant_id == id && m.is_available == true)
                             .OrderBy(m => m.category)
                             .ThenBy(m => m.name)
                             .ToList();
            ViewBag.MenuItems = menuItems;

            return View(restaurant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LuuDatBan(
            int restaurant_id,
            string booking_time,
            string booking_time_hour,
            int number_of_guests,
            string full_name,
            string phone,
            string special_request,
            string pre_order_note,
            string pre_order_total
        )
        {
            var restaurant = db.Restaurants.Find(restaurant_id);
            if (restaurant == null || !(restaurant.is_approved ?? false))
            {
                TempData["ErrorMessage"] = "Nhà hàng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            DateTime datePart;
            if (!DateTime.TryParse(booking_time, out datePart))
                datePart = DateTime.Now.Date;

            TimeSpan timePart;
            if (!TimeSpan.TryParse(booking_time_hour, out timePart))
                timePart = new TimeSpan(19, 0, 0); 

            var bookingDateTime = datePart.Date + timePart;
            var bookingDate = bookingDateTime.Date;

            if (bookingDateTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Thời gian đến phải lớn hơn thời gian hiện tại. Vui lòng chọn lại.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingDateTime,
                    NumberOfPeople = number_of_guests,
                    ReservationTime = timePart.ToString(@"hh\:mm")
                });
            }

            string startHourStr = "09:00";
            string endHourStr = "22:00";

            if (!string.IsNullOrEmpty(restaurant.opening_hours))
            {
                var parts = restaurant.opening_hours
                    .Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1) startHourStr = parts[0].Trim();
                if (parts.Length >= 2) endHourStr = parts[1].Trim();
            }

            TimeSpan openTime, closeTime;
            if (!TimeSpan.TryParse(startHourStr, out openTime))
                openTime = new TimeSpan(9, 0, 0);
            if (!TimeSpan.TryParse(endHourStr, out closeTime))
                closeTime = new TimeSpan(22, 0, 0);

            if (timePart < openTime || timePart > closeTime)
            {
                TempData["ErrorMessage"] =
                    $"Giờ này nhà hàng chưa mở cửa. Vui lòng chọn giờ trong khoảng {openTime:hh\\:mm} - {closeTime:hh\\:mm}.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingDateTime,
                    NumberOfPeople = number_of_guests,
                    ReservationTime = timePart.ToString(@"hh\:mm")
                });
            }

            var currentUser = Session["user"] as Users;
            int? userId = currentUser != null ? (int?)currentUser.user_id : null;

            if (userId == null)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để đặt bàn.";
                return RedirectToAction("Login", "Account");
            }

            var countToday = db.Bookings
                .Where(b => b.user_id == userId
                            && DbFunctions.TruncateTime(b.booking_time) == bookingDate)
                .Count();

            if (countToday >= 10)
            {
                TempData["ErrorMessage"] = "Mỗi ngày bạn chỉ được đặt tối đa 10 lần. Vui lòng chọn ngày khác hoặc đợi sang ngày hôm sau.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingDateTime,
                    NumberOfPeople = number_of_guests,
                    ReservationTime = timePart.ToString(@"hh\:mm")
                });
            }

            var totalCapacity = db.Tables
                .Where(t => t.restaurant_id == restaurant_id && t.is_available == true)
                .Select(t => (int?)t.capacity)
                .Sum() ?? 0;

            if (totalCapacity <= 0 && restaurant.max_tables.HasValue && restaurant.max_tables.Value > 0)
                totalCapacity = restaurant.max_tables.Value;

            if (totalCapacity <= 0)
            {
                TempData["ErrorMessage"] = "Nhà hàng chưa cấu hình số lượng bàn/chỗ ngồi. Vui lòng liên hệ nhà hàng.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingDateTime,
                    NumberOfPeople = number_of_guests,
                    ReservationTime = timePart.ToString(@"hh\:mm")
                });
            }

            var activeStatuses = new[] { "Đã cọc thành công", "Đã thanh toán", "Chờ xác nhận" };


            var totalBooked = db.Bookings
                .Where(b => b.restaurant_id == restaurant_id
                            && DbFunctions.TruncateTime(b.booking_time) == bookingDate
                            && activeStatuses.Contains(b.status))
                .Select(b => (int?)b.number_of_guests)
                .Sum() ?? 0;

            if (totalBooked + number_of_guests > totalCapacity)
            {
                TempData["ErrorMessage"] = "Nhà hàng đã đủ chỗ trong ngày này. Vui lòng chọn thời gian khác hoặc giảm số lượng khách.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingDateTime,
                    NumberOfPeople = number_of_guests,
                    ReservationTime = timePart.ToString(@"hh\:mm")
                });
            }

            decimal preOrderTotal = 0m;
            if (!string.IsNullOrWhiteSpace(pre_order_total))
            {
                decimal.TryParse(pre_order_total, NumberStyles.Any, CultureInfo.InvariantCulture, out preOrderTotal);
            }

            string finalNote = null;

            if (!string.IsNullOrWhiteSpace(special_request))
                finalNote = special_request.Trim();

            if (!string.IsNullOrWhiteSpace(pre_order_note))
            {
                var pre = pre_order_note.Trim();
                if (!string.IsNullOrEmpty(pre))
                {
                    if (!string.IsNullOrEmpty(finalNote))
                        finalNote += " | ";

                    finalNote += "Món chuẩn bị trước: " + pre;
                }
            }

            if (preOrderTotal > 0)
            {
                if (!string.IsNullOrEmpty(finalNote))
                    finalNote += " | ";

                finalNote += "Tiền món ước tính: " + preOrderTotal.ToString("N0") + " đ";
            }

            var booking = new Bookings
            {
                user_id = userId,
                restaurant_id = restaurant_id,
                booking_time = bookingDateTime,
                number_of_guests = number_of_guests,
                status = "Đã đặt",
                special_request = string.IsNullOrWhiteSpace(finalNote) ? null : finalNote
            };

            db.Bookings.Add(booking);
            db.SaveChanges();


            return RedirectToAction("Checkout", "ThanhToan", new
            {
                bookingId = booking.booking_id,
                preOrder = preOrderTotal,
                preOrderNote = pre_order_note      
            });
        }

        // ================== LỊCH SỬ ĐẶT BÀN CỦA NGƯỜI DÙNG ==================
        [HttpGet]
        public ActionResult DanhSachBanDaDat()
        {
            var user = Session["user"] as Users;
            if (user == null)
                return RedirectToAction("Login", "Account");

            int userId = user.user_id;

            var bookings = db.Bookings
                             .Include(b => b.Users)               
                             .Include(b => b.Restaurants)
                             .Include(b => b.Payments)
                             .Where(b => b.user_id == userId)
                             .OrderByDescending(b => b.booking_id)
                             .ToList();

            var list = new List<BookingHistoryItemViewModel>();

            foreach (var b in bookings)
            {
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
                    RestaurantId = b.restaurant_id ?? 0,   
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
                    PaymentStatus = paymentStatus,

                    CustomerName = b.Users != null ? b.Users.full_name : "Khách hàng SmartTable",
                    CustomerPhone = b.Users != null ? b.Users.phone : ""
                };

                list.Add(vm);
            }

            return View(list);
        }
        // ================== HỦY ĐẶT BÀN ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HuyDatBan(int bookingId)
        {
            var user = Session["user"] as Users;
            if (user == null)
                return RedirectToAction("Login", "Account");

            int userId = user.user_id;

            var booking = db.Bookings
                            .FirstOrDefault(b => b.booking_id == bookingId && b.user_id == userId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            var lastPayment = booking.Payments
                                     .OrderByDescending(p => p.payment_id)
                                     .FirstOrDefault();

            bool isPaid = lastPayment != null &&
                          !string.IsNullOrEmpty(lastPayment.status) &&
                          lastPayment.status.Contains("Đã thanh toán");

            if (isPaid)
            {
                TempData["ErrorMessage"] = "Đơn này đã thanh toán, vui lòng liên hệ nhà hàng để được hỗ trợ hủy.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            if (!string.IsNullOrEmpty(booking.status) &&
                booking.status.IndexOf("Hủy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TempData["ErrorMessage"] = "Đơn này đã được hủy trước đó.";
                return RedirectToAction("DanhSachBanDaDat");
            }


            booking.status = "Đã hủy bởi khách";
            booking.cancel_reason = "Khách tự hủy trên hệ thống";

            db.SaveChanges();

            TempData["SuccessMessage"] = "Bạn đã hủy đặt bàn #" + bookingId + " thành công.";
            return RedirectToAction("DanhSachBanDaDat");
        }

    }
}
