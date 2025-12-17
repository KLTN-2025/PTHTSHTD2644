using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class BusinessBookingsController : Controller
    {
        private readonly Entities db = new Entities();

        private const decimal DEPOSIT_PER_GUEST = 50000m;

        // Payment kind (chuẩn hoá)
        private const string KIND_DEPOSIT = "deposit";
        private const string KIND_REFUND_DEPOSIT = "refund_deposit";

        // Booking statuses (chuẩn hoá)
        private const string ST_PENDING = "Chờ xác nhận";
        private const string ST_BOOKED = "Đã đặt";
        private const string ST_DEPOSITED = "Đã cọc";
        private const string ST_PAID = "Đã thanh toán";
        private const string ST_CANCELED_BY_RESTAURANT = "Đã hủy bởi nhà hàng";

        // ===================== AUTH =====================
        private int? GetCurrentUserId()
        {
            var u = Session["user"] as Users;
            if (u != null && u.user_id > 0) return u.user_id;

            var raw = Session["user_id"] ?? Session["UserId"] ?? Session["USER_ID"];
            if (raw == null) return null;

            int id;
            return int.TryParse(raw.ToString(), out id) && id > 0 ? id : (int?)null;
        }

        private bool IsBusiness()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString().Trim();

            return role.Equals("Business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("DoanhNghiep", StringComparison.OrdinalIgnoreCase)
                || role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        // ===================== NOTIFICATIONS (USER SIDE) =====================
        // chỉ gửi cho khách hàng (user_id của khách)
        private void AddNotification_NoThrow(
            int userId,
            string type,
            string title,
            string message,
            int? restaurantId,
            int? bookingId,
            int? reviewId,
            string link)
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
            catch { /* không phá luồng chính */ }
        }

        // ===================== ROUTES =====================
        [HttpGet]
        public ActionResult Index(string statusFilter = null) => BuildBookingsView(statusFilter);

        [HttpGet]
        public ActionResult Bookings(string statusFilter = null) => BuildBookingsView(statusFilter);

        // ===================== BUILD VIEW =====================
        private ActionResult BuildBookingsView(string statusFilter)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var ownerId = GetCurrentUserId();
            if (!ownerId.HasValue) return RedirectToAction("Login", "Business");

            // Booking thuộc các nhà hàng của owner
            var query = db.Bookings.AsNoTracking()
                .Include(b => b.Restaurants)
                .Include(b => b.Users)
                .Include(b => b.Payments)
                .Where(b => b.Restaurants != null && b.Restaurants.user_id == ownerId.Value);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                var sf = statusFilter.Trim();
                query = query.Where(b => (b.status ?? "").Trim() == sf);
            }

            var bookings = query
                .OrderByDescending(b => b.booking_time)
                .ThenByDescending(b => b.booking_id)
                .ToList();

            ViewBag.StatusFilter = statusFilter;

            if (!bookings.Any())
                return View("Bookings", new List<BusinessBookingItemViewModel>());

            var bookingIds = bookings.Select(b => b.booking_id).ToList();

            // ===================== PREORDER (Orders + MenuItems) =====================
            var preOrders = (from o in db.Orders.AsNoTracking()
                             join m in db.MenuItems.AsNoTracking() on o.menu_item_id equals m.menu_item_id
                             where o.booking_id.HasValue && bookingIds.Contains(o.booking_id.Value)
                             select new
                             {
                                 BookingId = o.booking_id.Value,
                                 Name = m.name,
                                 Quantity = (int?)o.quantity ?? 0,
                                 Price = (decimal?)m.price ?? 0m
                             }).ToList();

            var preOrderByBooking = preOrders
                .GroupBy(x => x.BookingId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new BusinessBookingItemViewModel.PreOrderItemVm
                    {
                        Name = x.Name,
                        Quantity = x.Quantity,
                        UnitPrice = x.Price
                    }).ToList()
                );

            var preOrderTotalByBooking = preOrderByBooking.ToDictionary(
                x => x.Key,
                x => x.Value.Sum(i => i.UnitPrice * i.Quantity)
            );

            // ===================== VIEWMODEL =====================
            var result = new List<BusinessBookingItemViewModel>();

            foreach (var b in bookings)
            {
                int guests = b.number_of_guests > 0 ? b.number_of_guests : 1;

                // cọc dự kiến theo khách
                decimal expectedDeposit = guests * DEPOSIT_PER_GUEST;

                // payments
                var payments = b.Payments ?? new List<Payments>();
                decimal depositPaidSum = SumDepositPaid(payments);
                decimal depositRefundAbsSum = SumDepositRefundAbs(payments);
                decimal depositNet = Math.Max(0m, depositPaidSum - depositRefundAbsSum);

                bool isCanceled = IsCanceledBookingStatus(b.status);

                bool hasPreOrder = preOrderByBooking.ContainsKey(b.booking_id);
                var preorderItems = hasPreOrder ? preOrderByBooking[b.booking_id] : new List<BusinessBookingItemViewModel.PreOrderItemVm>();
                decimal preorderTotal = hasPreOrder ? preOrderTotalByBooking[b.booking_id] : 0m;

                string paymentStatus = BuildPaymentStatus(isCanceled, depositPaidSum, depositRefundAbsSum, depositNet);

                // hiển thị status booking rõ ràng
                string bookingStatus = NormalizeBookingStatus(b.status);

                result.Add(new BusinessBookingItemViewModel
                {
                    BookingId = b.booking_id,

                    RestaurantName = b.Restaurants?.name ?? "Nhà hàng",
                    RestaurantAddress = b.Restaurants?.address ?? "",
                    RestaurantImage = !string.IsNullOrEmpty(b.Restaurants?.Image) ? b.Restaurants.Image : "https://via.placeholder.com/120",

                    CustomerName = b.Users?.full_name ?? "Khách",
                    CustomerPhone = b.Users?.phone ?? "",

                    BookingTime = b.booking_time,
                    NumberOfGuests = guests,
                    SpecialRequest = b.special_request,

                    BookingStatus = bookingStatus,
                    CancelReason = b.cancel_reason,

                    // nghiệp vụ: expected vs net
                    DepositAmount = expectedDeposit,
                    DepositPaidAmount = depositNet,
                    PaymentStatus = paymentStatus,

                    HasPreOrder = hasPreOrder,
                    PreOrderItems = preorderItems,
                    PreOrderTotalAmount = preorderTotal,

                    // tên field hơi “lệch nghĩa”, nhưng giữ nguyên để không vỡ view:
                    // hiểu là "tổng dự kiến khách cần trả"
                    TotalCustomerPaidAmount = expectedDeposit + preorderTotal
                });
            }

            return View("Bookings", result);
        }

        // ===================== CONFIRM DEPOSIT (MANUAL) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(int bookingId)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var ownerId = GetCurrentUserId();
            if (!ownerId.HasValue) return RedirectToAction("Login", "Business");

            var booking = db.Bookings
                .Include(b => b.Restaurants)
                .Include(b => b.Users)
                .Include(b => b.Payments)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null || booking.Restaurants == null || booking.Restaurants.user_id != ownerId.Value)
            {
                TempData["ErrorMessage"] = "Không tìm thấy booking hoặc bạn không có quyền.";
                return RedirectToAction("Index");
            }

            if (IsCanceledBookingStatus(booking.status))
            {
                TempData["ErrorMessage"] = "Booking đã bị hủy. Không thể xác nhận cọc.";
                return RedirectToAction("Index");
            }

            int guests = booking.number_of_guests > 0 ? booking.number_of_guests : 1;
            decimal deposit = guests * DEPOSIT_PER_GUEST;

            var payments = booking.Payments ?? new List<Payments>();

            bool alreadyPaid = payments.Any(p => p != null
                && IsKind(p.payment_kind, KIND_DEPOSIT)
                && p.amount > 0m
                && IsPaidStatus(p.status));

            if (alreadyPaid)
            {
                TempData["ErrorMessage"] = $"Booking #{bookingId} đã có cọc trước đó.";
                return RedirectToAction("Index");
            }

            db.Payments.Add(new Payments
            {
                booking_id = bookingId,
                payment_kind = KIND_DEPOSIT,
                payment_method = "MANUAL",
                amount = deposit,
                status = "PAID",
                paid_at = DateTime.Now,
                transaction_id = "BANK-" + Guid.NewGuid().ToString("N")
            });

            // ✅ chuẩn nghiệp vụ: chuyển trạng thái booking sang "Đã cọc"
            var st = NormalizeBookingStatus(booking.status);
            if (st == ST_PENDING || st == ST_BOOKED)
                booking.status = ST_DEPOSITED;

            // ✅ thông báo cho khách
            var targetUserId = booking.user_id;
            if (targetUserId.HasValue && targetUserId.Value > 0)
            {
                AddNotification_NoThrow(
                    userId: targetUserId.Value,
                    type: "deposit_confirmed",
                    title: "Nhà hàng đã xác nhận tiền cọc",
                    message: $"Booking #{booking.booking_id} tại {booking.Restaurants?.name ?? "nhà hàng"} đã được xác nhận cọc {deposit:N0}đ.",
                    restaurantId: booking.restaurant_id,
                    bookingId: booking.booking_id,
                    reviewId: null,
                    link: Url.Action("DanhSachBanDaDat", "DatBan")
                );
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xác nhận cọc booking #{bookingId}.";
            return RedirectToAction("Index");
        }

        // ===================== CANCEL (NO REFUND HERE) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(int bookingId, string cancelReason)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var ownerId = GetCurrentUserId();
            if (!ownerId.HasValue) return RedirectToAction("Login", "Business");

            var booking = db.Bookings
                .Include(b => b.Restaurants)
                .Include(b => b.Users)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null || booking.Restaurants == null || booking.Restaurants.user_id != ownerId.Value)
            {
                TempData["ErrorMessage"] = "Không tìm thấy booking hoặc bạn không có quyền.";
                return RedirectToAction("Index");
            }

            // nghiệp vụ: chỉ cho huỷ trước giờ
            if (booking.booking_time <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Đã quá thời gian hủy.";
                return RedirectToAction("Index");
            }

            var reason = string.IsNullOrWhiteSpace(cancelReason) ? "Nhà hàng hủy" : cancelReason.Trim();

            booking.status = ST_CANCELED_BY_RESTAURANT;
            booking.cancel_reason = reason;

            // ✅ thông báo cho khách
            var targetUserId = booking.user_id;
            if (targetUserId.HasValue && targetUserId.Value > 0)
            {
                AddNotification_NoThrow(
                    userId: targetUserId.Value,
                    type: "booking_canceled_by_restaurant",
                    title: "Nhà hàng đã hủy đặt bàn",
                    message: $"Booking #{booking.booking_id} tại {booking.Restaurants?.name ?? "nhà hàng"} đã bị hủy. Lý do: {reason}",
                    restaurantId: booking.restaurant_id,
                    bookingId: booking.booking_id,
                    reviewId: null,
                    link: Url.Action("DanhSachBanDaDat", "DatBan")
                );
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hủy booking #{bookingId}.";
            return RedirectToAction("Index");
        }

        // ===================== HELPERS =====================
        private string NormalizeBookingStatus(string status)
        {
            var st = (status ?? "").Trim();
            if (string.IsNullOrWhiteSpace(st)) return ST_PENDING;

            // chuẩn hoá một số status tiếng Anh nếu có
            if (st.Equals("confirmed", StringComparison.OrdinalIgnoreCase)) return ST_BOOKED;
            if (st.Equals("checked-in", StringComparison.OrdinalIgnoreCase)) return ST_BOOKED;

            return st;
        }

        private string BuildPaymentStatus(bool isCanceled, decimal depositPaidSum, decimal refundAbsSum, decimal depositNet)
        {
            bool hasEverPaid = depositPaidSum > 0m;
            bool hasAnyRefund = refundAbsSum > 0m;
            bool isRefundedFull = hasEverPaid && hasAnyRefund && depositNet == 0m;
            bool isPaid = depositNet > 0m;

            if (isCanceled)
            {
                if (isRefundedFull) return "Đã hủy (đã hoàn cọc)";
                if (isPaid) return "Đã hủy (chưa hoàn cọc)";
                return "Đã hủy";
            }

            if (isPaid) return "Đã cọc";
            return "Chưa cọc";
        }

        private decimal SumDepositPaid(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return 0m;

            return payments
                .Where(p => p != null
                    && p.amount > 0m
                    && IsKind(p.payment_kind, KIND_DEPOSIT)
                    && IsPaidStatus(p.status))
                .Sum(p => p.amount);
        }

        private decimal SumDepositRefundAbs(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return 0m;

            return payments
                .Where(p => p != null
                    && p.amount < 0m
                    && IsKind(p.payment_kind, KIND_REFUND_DEPOSIT))
                .Sum(p => Math.Abs(p.amount));
        }

        private bool IsKind(string kind, string expected)
        {
            if (string.IsNullOrWhiteSpace(kind)) return false;
            return kind.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPaidStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
                || status.IndexOf("thành công", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCanceledBookingStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.IndexOf("hủy", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("huỷ", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
