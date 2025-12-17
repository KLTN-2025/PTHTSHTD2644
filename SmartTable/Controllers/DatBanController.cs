using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    public class DatBanController : Controller
    {
        private readonly Entities db = new Entities();

        private const int DefaultDurationMinutes = 90;
        private const decimal DEPOSIT_PER_GUEST = 50000m;

        private const string NOTI_USER_BOOKING_CREATED = "BOOKING_CREATED";
        private const string NOTI_BIZ_BOOKING_CREATED = "BIZ_BOOKING_CREATED";
        private const string NOTI_USER_BOOKING_CANCELED_BY_USER = "BOOKING_CANCELED_BY_USER";
        private const string NOTI_BIZ_BOOKING_CANCELED_BY_USER = "BIZ_BOOKING_CANCELED_BY_USER";

        private static readonly string[] ActiveBookingStatuses = new[]
        {
            "Đã đặt",
            "Chờ xác nhận",
            "Đã cọc",
            "Đã cọc thành công",
            "Đã thanh toán",
            "confirmed",
            "checked-in"
        };

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

            ViewBag.PreSelectedDate = (ReservationDate ?? DateTime.Today).Date;
            ViewBag.PreSelectedPeople = NumberOfPeople ?? 2;
            ViewBag.PreSelectedTime = ReservationTime;

            var menuItems = db.MenuItems
                .AsNoTracking()
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
            string pre_order_total,
            int[] menu_item_ids,
            int[] quantities,
            string[] notes
        )
        {
            var restaurant = db.Restaurants.FirstOrDefault(r => r.restaurant_id == restaurant_id);
            if (restaurant == null || !(restaurant.is_approved ?? false))
            {
                TempData["ErrorMessage"] = "Nhà hàng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            var currentUser = Session["user"] as Users;
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để đặt bàn.";
                return RedirectToAction("Login", "Account");
            }

            int userId = currentUser.user_id;
            int ownerId = restaurant.user_id.GetValueOrDefault(0);

            int guests = number_of_guests <= 0 ? 1 : number_of_guests;

            DateTime bookingStart = BuildBookingDateTime(booking_time, booking_time_hour);
            DateTime bookingEnd = bookingStart.AddMinutes(DefaultDurationMinutes);

            if (bookingStart <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Thời gian đến phải lớn hơn thời gian hiện tại. Vui lòng chọn lại.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingStart.Date,
                    NumberOfPeople = guests,
                    ReservationTime = bookingStart.ToString("HH:mm")
                });
            }

            if (!IsWithinOpeningHours(restaurant.opening_hours, bookingStart.TimeOfDay, bookingEnd.TimeOfDay, out string openHoursMsg))
            {
                TempData["ErrorMessage"] = openHoursMsg;
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingStart.Date,
                    NumberOfPeople = guests,
                    ReservationTime = bookingStart.ToString("HH:mm")
                });
            }

            var dayStart = bookingStart.Date;
            var dayEnd = dayStart.AddDays(1);

            int countToday = db.Bookings.Count(b =>
                b.user_id == userId &&
                b.booking_time >= dayStart && b.booking_time < dayEnd
            );

            if (countToday >= 10)
            {
                TempData["ErrorMessage"] = "Mỗi ngày bạn chỉ được đặt tối đa 10 lần. Vui lòng chọn ngày khác hoặc đợi sang ngày hôm sau.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingStart.Date,
                    NumberOfPeople = guests,
                    ReservationTime = bookingStart.ToString("HH:mm")
                });
            }

            if (IsUserOverlapping(userId, restaurant_id, bookingStart, bookingEnd))
            {
                TempData["ErrorMessage"] = "Bạn đã có một lịch đặt trùng khung giờ này tại nhà hàng. Vui lòng chọn giờ khác.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingStart.Date,
                    NumberOfPeople = guests,
                    ReservationTime = bookingStart.ToString("HH:mm")
                });
            }

            var assignedTable = AutoAssignTableStrict(restaurant_id, guests, bookingStart, DefaultDurationMinutes);
            if (assignedTable == null)
            {
                TempData["ErrorMessage"] = "Hiện không còn bàn phù hợp vào thời gian này. Vui lòng chọn giờ khác hoặc giảm số khách.";
                return RedirectToAction("DatBan", new
                {
                    id = restaurant_id,
                    ReservationDate = bookingStart.Date,
                    NumberOfPeople = guests,
                    ReservationTime = bookingStart.ToString("HH:mm")
                });
            }

            decimal preOrderTotal = ParseDecimalSafe(pre_order_total);
            string finalNote = BuildFinalNote(special_request);

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var dbUser = db.Users.FirstOrDefault(u => u.user_id == userId);
                    if (dbUser != null)
                    {
                        if (!string.IsNullOrWhiteSpace(full_name)) dbUser.full_name = full_name.Trim();
                        if (!string.IsNullOrWhiteSpace(phone)) dbUser.phone = phone.Trim();
                    }

                    var booking = new Bookings
                    {
                        user_id = userId,
                        restaurant_id = restaurant_id,
                        created_at = DateTime.Now,
                        booking_time = bookingStart,
                        number_of_guests = guests,
                        status = "Đã đặt",
                        table_id = assignedTable.table_id,
                        special_request = finalNote
                    };

                    db.Bookings.Add(booking);
                    db.SaveChanges();

                    SavePreOrdersToOrdersTable(restaurant_id, booking.booking_id, menu_item_ids, quantities, notes);

                    AddNotification_NoThrow(
                        userId: userId,
                        type: NOTI_USER_BOOKING_CREATED,
                        title: "Đã nhận yêu cầu đặt bàn",
                        message: $"Bạn đã đặt bàn tại {restaurant.name} lúc {bookingStart:HH:mm dd/MM/yyyy}. Vui lòng chờ nhà hàng xác nhận.",
                        restaurantId: restaurant_id,
                        bookingId: booking.booking_id,
                        reviewId: null,
                        link: Url.Action("DanhSachBanDaDat", "DatBan")
                    );

                    if (ownerId > 0)
                    {
                        AddNotification_NoThrow(
                            userId: ownerId,
                            type: NOTI_BIZ_BOOKING_CREATED,
                            title: "Có yêu cầu đặt bàn mới",
                            message: $"Booking #{booking.booking_id} - {guests} khách lúc {bookingStart:HH:mm dd/MM/yyyy}.",
                            restaurantId: restaurant_id,
                            bookingId: booking.booking_id,
                            reviewId: null,
                            link: Url.Action("Bookings", "BusinessBookings")
                        );
                    }

                    db.SaveChanges();
                    tx.Commit();

                    return RedirectToAction("Checkout", "ThanhToan", new
                    {
                        bookingId = booking.booking_id,
                        preOrder = preOrderTotal,
                        preOrderNote = pre_order_note
                    });
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    System.Diagnostics.Debug.WriteLine("LuuDatBan Error: " + ex);

                    TempData["ErrorMessage"] = "Có lỗi khi lưu đặt bàn. Vui lòng thử lại hoặc liên hệ hỗ trợ.";
                    return RedirectToAction("DatBan", new
                    {
                        id = restaurant_id,
                        ReservationDate = bookingStart.Date,
                        NumberOfPeople = guests,
                        ReservationTime = bookingStart.ToString("HH:mm")
                    });
                }
            }
        }

        [HttpGet]
        public ActionResult DanhSachBanDaDat()
        {
            var user = Session["user"] as Users;
            if (user == null) return RedirectToAction("Login", "Account");

            int userId = user.user_id;

            var bookings = db.Bookings
                .AsNoTracking()
                .Include(b => b.Users)
                .Include(b => b.Restaurants)
                .Include(b => b.Payments)
                .Where(b => b.user_id == userId)
                .OrderByDescending(b => b.booking_id)
                .ToList();

            if (!bookings.Any())
                return View(new List<BookingHistoryItemViewModel>());

            var bookingIds = bookings.Select(b => b.booking_id).ToList();

            var allOrders = db.Orders.AsNoTracking()
                .Where(o => o.booking_id.HasValue && bookingIds.Contains(o.booking_id.Value))
                .ToList();

            var menuIds = allOrders
                .Select(o => (int?)o.menu_item_id)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x.Value)
                .Distinct()
                .ToList();

            var allMenus = db.MenuItems.AsNoTracking()
                .Where(m => menuIds.Contains(m.menu_item_id))
                .ToList();

            var menuMap = allMenus.ToDictionary(m => m.menu_item_id, m => m);

            var ordersByBooking = allOrders
                .GroupBy(o => o.booking_id.GetValueOrDefault(0))
                .ToDictionary(g => g.Key, g => g.ToList());

            var list = new List<BookingHistoryItemViewModel>();

            foreach (var b in bookings)
            {
                int guests = (b.number_of_guests <= 0) ? 1 : b.number_of_guests;
                decimal expectedDeposit = guests * DEPOSIT_PER_GUEST;

                decimal depositPaidSum = SumDepositPaid(b.Payments);
                decimal depositRefundSum = SumDepositRefund(b.Payments);
                decimal depositNet = Math.Max(0m, depositPaidSum - depositRefundSum);

                bool isCanceled = IsCanceledBookingStatus(b.status);

                var latestPaidDeposit = GetLatestDepositPaidPayment(b.Payments);
                var lastPayment = b.Payments?.OrderByDescending(p => p.payment_id).FirstOrDefault();
                string methodLabel = DetectMethod(latestPaidDeposit ?? lastPayment);

                string paymentStatus;
                if (depositPaidSum <= 0m) paymentStatus = "Chưa cọc";
                else if (depositNet == 0m && depositRefundSum > 0m) paymentStatus = "Đã hoàn cọc";
                else if (depositNet > 0m) paymentStatus = "Đã cọc thành công";
                else paymentStatus = "Chờ xác nhận";

                if (isCanceled)
                {
                    if (depositPaidSum > 0m && depositNet == 0m && depositRefundSum > 0m) paymentStatus = "Đã hủy (đã hoàn cọc)";
                    else if (depositNet > 0m) paymentStatus = "Đã hủy (chưa hoàn cọc)";
                    else paymentStatus = "Đã hủy";
                }

                var preItems = new List<BookingHistoryItemViewModel.PreOrderItemVm>();
                decimal preTotal = 0m;

                if (ordersByBooking.TryGetValue(b.booking_id, out var ordersOfBooking) && ordersOfBooking != null)
                {
                    foreach (var o in ordersOfBooking)
                    {
                        int mid = (int?)o.menu_item_id ?? 0;
                        if (mid <= 0) continue;

                        if (!menuMap.TryGetValue(mid, out var menu) || menu == null) continue;

                        string name = (menu.name ?? "").Trim();
                        decimal unitPrice = 0m;
                        try { unitPrice = Convert.ToDecimal(menu.price); } catch { unitPrice = 0m; }

                        int qty = (o.quantity <= 0) ? 1 : o.quantity;

                        preItems.Add(new BookingHistoryItemViewModel.PreOrderItemVm
                        {
                            Name = string.IsNullOrWhiteSpace(name) ? "Món ăn" : name,
                            Quantity = qty,
                            UnitPrice = unitPrice
                        });

                        preTotal += unitPrice * qty;
                    }
                }

                list.Add(new BookingHistoryItemViewModel
                {
                    BookingId = b.booking_id,
                    RestaurantId = b.restaurant_id ?? 0,
                    RestaurantName = b.Restaurants?.name ?? "Nhà hàng",
                    RestaurantAddress = b.Restaurants?.address ?? "",
                    RestaurantImage = !string.IsNullOrEmpty(b.Restaurants?.Image) ? b.Restaurants.Image : "https://via.placeholder.com/120",

                    CreatedAt = b.created_at,
                    BookingTime = b.booking_time,
                    NumberOfGuests = guests,
                    SpecialRequest = b.special_request,

                    BookingStatus = string.IsNullOrWhiteSpace(b.status) ? "Chờ xác nhận" : b.status,
                    CancelReason = b.cancel_reason,

                    DepositAmount = expectedDeposit,
                    PaymentStatus = paymentStatus,
                    PaymentMethod = methodLabel,

                    HasPreOrder = preItems.Any(),
                    PreOrderItems = preItems,
                    PreOrderTotalAmount = preTotal,

                    CustomerName = b.Users?.full_name ?? "Khách hàng SmartTable",
                    CustomerPhone = b.Users?.phone ?? ""
                });
            }

            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HuyDatBan(int bookingId)
        {
            var user = Session["user"] as Users;
            if (user == null) return RedirectToAction("Login", "Account");

            int userId = user.user_id;

            var booking = db.Bookings
                .Include(b => b.Payments)
                .Include(b => b.Restaurants)
                .FirstOrDefault(b => b.booking_id == bookingId && b.user_id == userId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            if (booking.booking_time <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Đã quá thời gian hẹn. Bạn không thể hủy online. Vui lòng liên hệ nhà hàng.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            if (IsCanceledBookingStatus(booking.status))
            {
                TempData["ErrorMessage"] = "Đơn này đã được hủy trước đó.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            decimal paid = SumDepositPaid(booking.Payments);
            decimal refunded = SumDepositRefund(booking.Payments);
            decimal net = Math.Max(0m, paid - refunded);

            if (net > 0m)
            {
                TempData["ErrorMessage"] = "Đơn này đã có cọc. Vui lòng liên hệ nhà hàng để được hỗ trợ hủy/hoàn cọc theo chính sách.";
                return RedirectToAction("DanhSachBanDaDat");
            }

            booking.status = "Đã hủy bởi khách";
            booking.cancel_reason = "Khách tự hủy trên hệ thống";

            AddNotification_NoThrow(
                userId: userId,
                type: NOTI_USER_BOOKING_CANCELED_BY_USER,
                title: "Bạn đã hủy đặt bàn",
                message: $"Bạn đã hủy đặt bàn #{booking.booking_id} tại {(booking.Restaurants?.name ?? "nhà hàng")} thành công.",
                restaurantId: booking.restaurant_id,
                bookingId: booking.booking_id,
                reviewId: null,
                link: Url.Action("DanhSachBanDaDat", "DatBan")
            );

            int ownerId = booking.Restaurants?.user_id.GetValueOrDefault(0) ?? 0;
            if (ownerId > 0)
            {
                AddNotification_NoThrow(
                    userId: ownerId,
                    type: NOTI_BIZ_BOOKING_CANCELED_BY_USER,
                    title: "Khách đã hủy đặt bàn",
                    message: $"Booking #{booking.booking_id} đã bị khách hủy. Thời gian: {booking.booking_time:HH:mm dd/MM/yyyy}.",
                    restaurantId: booking.restaurant_id,
                    bookingId: booking.booking_id,
                    reviewId: null,
                    link: Url.Action("Bookings", "BusinessBookings")
                );
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = "Bạn đã hủy đặt bàn #" + bookingId + " thành công.";
            return RedirectToAction("DanhSachBanDaDat");
        }

        private void AddNotification_NoThrow(int userId, string type, string title, string message,
                                             int? restaurantId, int? bookingId, int? reviewId, string link)
        {
            try
            {
                if (userId <= 0) return;

                db.Notifications.Add(new Notifications
                {
                    user_id = userId,
                    restaurant_id = restaurantId,
                    booking_id = bookingId,
                    review_id = reviewId,
                    type = string.IsNullOrWhiteSpace(type) ? "system" : type.Trim(),
                    title = string.IsNullOrWhiteSpace(title) ? "Thông báo" : title.Trim(),
                    message = message ?? "",
                    link = link,
                    is_read = false,
                    created_at = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddNotification_NoThrow Error: " + ex);
            }
        }

        private DateTime BuildBookingDateTime(string dateStr, string timeStr)
        {
            DateTime datePart;
            if (!DateTime.TryParse(dateStr, out datePart)) datePart = DateTime.Now.Date;

            TimeSpan timePart;
            if (!TimeSpan.TryParse(timeStr, out timePart)) timePart = new TimeSpan(19, 0, 0);

            return datePart.Date + timePart;
        }

        private bool IsWithinOpeningHours(string openingHours, TimeSpan start, TimeSpan end, out string message)
        {
            var open = new TimeSpan(9, 0, 0);
            var close = new TimeSpan(22, 0, 0);

            if (!string.IsNullOrWhiteSpace(openingHours))
            {
                var parts = openingHours.Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1) TimeSpan.TryParse(parts[0].Trim(), out open);
                if (parts.Length >= 2) TimeSpan.TryParse(parts[1].Trim(), out close);
            }

            if (start < open || start >= close || end > close)
            {
                message = $"Giờ đặt không hợp lệ. Vui lòng chọn trong khoảng {open:hh\\:mm} - {close:hh\\:mm}.";
                return false;
            }

            message = null;
            return true;
        }

        private bool IsUserOverlapping(int userId, int restaurantId, DateTime start, DateTime end)
        {
            var dayStart = start.Date;
            var dayEnd = dayStart.AddDays(1);

            var candidates = db.Bookings
                .Where(b =>
                    b.user_id == userId &&
                    b.restaurant_id == restaurantId &&
                    (b.status == null || ActiveBookingStatuses.Contains(b.status)) &&
                    b.booking_time >= dayStart && b.booking_time < dayEnd
                )
                .Select(b => b.booking_time)
                .ToList();

            foreach (var bStart in candidates)
            {
                var bEnd = bStart.AddMinutes(DefaultDurationMinutes);
                if (bStart < end && bEnd > start) return true;
            }

            return false;
        }

        private Tables AutoAssignTableStrict(int restaurantId, int guests, DateTime bookingStart, int durationMinutes)
        {
            var bookingEnd = bookingStart.AddMinutes(durationMinutes);

            var tables = db.Tables
                .Where(t =>
                    t.restaurant_id == restaurantId &&
                    (t.is_available == null || t.is_available == true) &&
                    t.capacity >= guests
                )
                .OrderBy(t => t.capacity)
                .ToList();

            if (!tables.Any()) return null;

            var dayStart = bookingStart.Date;
            var dayEnd = dayStart.AddDays(1);

            var activeBookings = db.Bookings
                .Where(b =>
                    b.restaurant_id == restaurantId &&
                    (b.status == null || ActiveBookingStatuses.Contains(b.status)) &&
                    b.table_id != null &&
                    b.booking_time >= dayStart && b.booking_time < dayEnd
                )
                .Select(b => new { b.table_id, b.booking_time })
                .ToList();

            foreach (var t in tables)
            {
                bool hasConflict = activeBookings
                    .Where(x => x.table_id == t.table_id)
                    .Any(x =>
                    {
                        var bStart = x.booking_time;
                        var bEnd = bStart.AddMinutes(durationMinutes);
                        return bStart < bookingEnd && bEnd > bookingStart;
                    });

                if (!hasConflict) return t;
            }

            return null;
        }

        private decimal ParseDecimalSafe(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0m;

            var s = input.Trim()
                .Replace("đ", "")
                .Replace("Đ", "")
                .Replace(".", "")
                .Replace(",", "")
                .Trim();

            decimal val;
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val) ? val : 0m;
        }

        private string BuildFinalNote(string userNote)
        {
            if (string.IsNullOrWhiteSpace(userNote)) return null;
            return userNote.Trim();
        }

        private void SavePreOrdersToOrdersTable(int restaurantId, int bookingId, int[] menuIds, int[] qtys, string[] notes)
        {
            if (menuIds == null || qtys == null) return;
            if (menuIds.Length != qtys.Length) return;

            var pairs = menuIds
                .Select((id, idx) => new { id, idx })
                .Where(x => x.id > 0 && qtys[x.idx] > 0)
                .ToList();

            if (!pairs.Any()) return;

            var validMenuIds = db.MenuItems.AsNoTracking()
                .Where(m => m.restaurant_id == restaurantId && m.is_available == true && menuIds.Contains(m.menu_item_id))
                .Select(m => m.menu_item_id)
                .ToList();

            var validSet = new HashSet<int>(validMenuIds);

            foreach (var x in pairs)
            {
                int menuId = x.id;
                int qty = qtys[x.idx];
                if (!validSet.Contains(menuId)) continue;

                db.Orders.Add(new Orders
                {
                    booking_id = bookingId,
                    menu_item_id = menuId,
                    quantity = qty,
                    note = (notes != null && notes.Length > x.idx) ? notes[x.idx] : null
                });
            }
        }

        private decimal SumDepositPaid(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return 0m;

            return payments
                .Where(p => p != null && IsDepositPayment(p) && GetDecimal(p, "amount", "Amount") > 0m && IsPaidPaymentStatus(GetString(p, "status", "Status")))
                .Sum(p => GetDecimal(p, "amount", "Amount"));
        }

        private decimal SumDepositRefund(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return 0m;

            var sumAbs = payments
                .Where(p => p != null && IsRefundDepositPayment(p) && IsRefundPaymentStatus(GetString(p, "status", "Status")))
                .Sum(p =>
                {
                    var amt = GetDecimal(p, "amount", "Amount");
                    return amt < 0m ? Math.Abs(amt) : amt;
                });

            return sumAbs;
        }

        private Payments GetLatestDepositPaidPayment(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return null;

            return payments
                .Where(p => p != null && IsDepositPayment(p) && GetDecimal(p, "amount", "Amount") > 0m && IsPaidPaymentStatus(GetString(p, "status", "Status")))
                .OrderByDescending(p => GetInt(p, "payment_id", "PaymentId"))
                .FirstOrDefault();
        }

        private string DetectMethod(Payments pay)
        {
            if (pay == null) return "Nhà hàng xác nhận";

            var vnp = (GetString(pay, "vnp_TransactionNo", "vnp_TxnRef", "vnp_BankCode", "vnp_BankTranNo") ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(vnp)) return "VNPAY";

            var method = (GetString(pay, "payment_method", "method", "PaymentMethod") ?? "").ToLowerInvariant();
            if (method.Contains("vnpay")) return "VNPAY";
            if (method.Contains("cash") || method.Contains("tiền mặt")) return "Tiền mặt";
            if (method.Contains("bank") || method.Contains("transfer") || method.Contains("chuyển")) return "Chuyển khoản";

            return "Nhà hàng xác nhận";
        }

        private bool IsDepositPayment(Payments p)
        {
            var kind = (GetString(p, "payment_kind", "kind", "PaymentKind") ?? "").Trim().ToLowerInvariant();
            if (kind == "deposit") return true;

            var st = (GetString(p, "status", "Status") ?? "").ToLowerInvariant();
            return st.Contains("cọc") || st.Contains("deposit") || st.Contains("giữ chỗ") || st.Contains("hold");
        }

        private bool IsRefundDepositPayment(Payments p)
        {
            var kind = (GetString(p, "payment_kind", "kind", "PaymentKind") ?? "").Trim().ToLowerInvariant();
            if (kind == "refund_deposit" || kind == "refund") return true;

            var st = (GetString(p, "status", "Status") ?? "").ToLowerInvariant();
            return st.Contains("hoàn") || st.Contains("refund") || st.Contains("reverse") || st.Contains("reversed");
        }

        private bool IsPaidPaymentStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.IndexOf("thành công", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("đã cọc", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("đã thanh toán", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("paid", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsRefundPaymentStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.IndexOf("hoàn", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("reversed", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("reverse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCanceledBookingStatus(string bookingStatus)
        {
            if (string.IsNullOrWhiteSpace(bookingStatus)) return false;
            bookingStatus = bookingStatus.Trim();

            return bookingStatus.IndexOf("đã hủy", StringComparison.OrdinalIgnoreCase) >= 0
                || bookingStatus.IndexOf("hủy", StringComparison.OrdinalIgnoreCase) >= 0
                || bookingStatus.IndexOf("huỷ", StringComparison.OrdinalIgnoreCase) >= 0
                || bookingStatus.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetString(object obj, params string[] props)
        {
            if (obj == null || props == null) return null;
            var t = obj.GetType();
            foreach (var p in props)
            {
                var pi = t.GetProperty(p);
                if (pi == null) continue;
                var v = pi.GetValue(obj, null);
                if (v == null) continue;
                var s = v.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        private decimal GetDecimal(object obj, params string[] props)
        {
            var s = GetString(obj, props);
            if (string.IsNullOrWhiteSpace(s))
            {
                try
                {
                    var t = obj.GetType();
                    foreach (var p in props)
                    {
                        var pi = t.GetProperty(p);
                        if (pi == null) continue;
                        var v = pi.GetValue(obj, null);
                        if (v == null) continue;
                        return Convert.ToDecimal(v);
                    }
                }
                catch { return 0m; }
                return 0m;
            }

            decimal d;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out d)) return d;
            return 0m;
        }

        private int GetInt(object obj, params string[] props)
        {
            var s = GetString(obj, props);
            if (string.IsNullOrWhiteSpace(s)) return 0;

            int i;
            if (int.TryParse(s, out i)) return i;

            try
            {
                var t = obj.GetType();
                foreach (var p in props)
                {
                    var pi = t.GetProperty(p);
                    if (pi == null) continue;
                    var v = pi.GetValue(obj, null);
                    if (v == null) continue;
                    return Convert.ToInt32(v);
                }
            }
            catch { }

            return 0;
        }
    }
}
