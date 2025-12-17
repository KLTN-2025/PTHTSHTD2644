using SmartTable.Filters;
using SmartTable.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class RefundController : Controller
    {
        private readonly Entities db = new Entities();

        private const decimal DEPOSIT_PER_GUEST = 50000m;

        private const string KIND_DEPOSIT = "deposit";
        private const string KIND_REFUND_DEPOSIT = "refund_deposit";

        private const string NOTI_USER_REFUND = "DEPOSIT_REFUNDED";
        private const string NOTI_BIZ_REFUND = "BIZ_DEPOSIT_REFUNDED";

        [HttpGet]
        public ActionResult Index(int bookingId, string returnUrl)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var ownerId = GetCurrentUserId();
            if (!ownerId.HasValue) return RedirectToAction("Login", "Business");

            if (bookingId <= 0) return HttpNotFound();

            var booking = db.Bookings
                .Include("Restaurants")
                .Include("Users")
                .Include("Payments")
                .FirstOrDefault(b =>
                    b.booking_id == bookingId &&
                    b.Restaurants != null &&
                    b.Restaurants.user_id == ownerId.Value);

            if (booking == null) return HttpNotFound();

            bool hasPreOrder = db.Orders.AsNoTracking()
                .Any(o => o.booking_id.HasValue && o.booking_id.Value == booking.booking_id);

            var payments = booking.Payments ?? new List<Payments>();
            decimal depositNet = GetDepositNet(payments);

            var paidDeposit = GetLatestPaidDeposit(payments);
            var refundedDeposit = GetLatestRefundDeposit(payments);

            bool hasPaid = paidDeposit != null;
            bool hasRefunded = refundedDeposit != null && IsRefundAfterPaid(paidDeposit, refundedDeposit);

            bool canRefund = !hasPreOrder && depositNet > 0m && hasPaid && !hasRefunded;

            ViewBag.BookingId = booking.booking_id;
            ViewBag.RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "";
            ViewBag.BookingTime = booking.booking_time;
            ViewBag.NumberOfGuests = booking.number_of_guests;
            ViewBag.BookingStatus = booking.status ?? "";
            ViewBag.DepositAmount = CalcDeposit(booking);
            ViewBag.DepositNet = depositNet;

            ViewBag.HasPreOrder = hasPreOrder;
            ViewBag.HasPaid = hasPaid;
            ViewBag.HasRefunded = hasRefunded;
            ViewBag.CanRefund = canRefund;

            ViewBag.DepositPaidAt = paidDeposit != null ? paidDeposit.paid_at : null;
            ViewBag.DepositRefundedAt = refundedDeposit != null ? refundedDeposit.paid_at : null;

            ViewBag.ReturnUrl = NormalizeReturnUrl(returnUrl);

            return View("~/Views/BusinessRefund/Index.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmRefund(int bookingId, string returnUrl)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var ownerId = GetCurrentUserId();
            if (!ownerId.HasValue) return RedirectToAction("Login", "Business");

            if (bookingId <= 0) return HttpNotFound();

            var booking = db.Bookings
                .Include("Restaurants")
                .Include("Users")
                .Include("Payments")
                .FirstOrDefault(b =>
                    b.booking_id == bookingId &&
                    b.Restaurants != null &&
                    b.Restaurants.user_id == ownerId.Value);

            if (booking == null) return HttpNotFound();

            bool hasPreOrder = db.Orders.AsNoTracking()
                .Any(o => o.booking_id.HasValue && o.booking_id.Value == booking.booking_id);

            if (hasPreOrder)
            {
                TempData["ErrorMessage"] = $"Booking #{booking.booking_id} có đặt món nên không cho hoàn cọc.";
                return RedirectBack(returnUrl);
            }

            var payments = booking.Payments ?? new List<Payments>();
            var paidDeposit = GetLatestPaidDeposit(payments);
            if (paidDeposit == null)
            {
                TempData["ErrorMessage"] = $"Booking #{booking.booking_id} chưa có cọc hợp lệ nên không thể hoàn.";
                return RedirectBack(returnUrl);
            }

            var refundedDeposit = GetLatestRefundDeposit(payments);
            if (IsRefundAfterPaid(paidDeposit, refundedDeposit))
            {
                TempData["ErrorMessage"] = $"Booking #{booking.booking_id} đã hoàn cọc trước đó.";
                return RedirectBack(returnUrl);
            }

            decimal refundable = GetDepositNet(payments);
            if (refundable <= 0m)
            {
                TempData["ErrorMessage"] = "Không còn tiền cọc để hoàn (net = 0).";
                return RedirectBack(returnUrl);
            }

            db.Payments.Add(new Payments
            {
                booking_id = booking.booking_id,
                amount = -refundable,
                payment_method = "REFUND_MANUAL",
                status = "REFUNDED",
                payment_kind = KIND_REFUND_DEPOSIT,
                paid_at = DateTime.Now,
                transaction_id = "REFUND-" + Guid.NewGuid().ToString("N")
            });

            if (!IsCanceledBookingStatus(booking.status))
                booking.status = "Đã hủy (đã hoàn cọc)";

            booking.cancel_reason = ((booking.cancel_reason ?? "").Trim() + " | Hoàn cọc").Trim();

            AddNotification_NoThrow(
                userId: booking.user_id.GetValueOrDefault(0),
                type: NOTI_USER_REFUND,
                title: "Bạn đã được hoàn cọc",
                message: $"Booking #{booking.booking_id} tại {(booking.Restaurants?.name ?? "nhà hàng")} đã được hoàn {refundable:N0}đ.",
                restaurantId: booking.restaurant_id,
                bookingId: booking.booking_id,
                reviewId: null,
                link: Url.Action("DanhSachBanDaDat", "DatBan")
            );

            AddNotification_NoThrow(
                userId: ownerId.Value,
                type: NOTI_BIZ_REFUND,
                title: "Đã hoàn cọc cho khách",
                message: $"Booking #{booking.booking_id} đã hoàn {refundable:N0}đ.",
                restaurantId: booking.restaurant_id,
                bookingId: booking.booking_id,
                reviewId: null,
                link: Url.Action("Bookings", "BusinessBookings")
            );

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Đã hoàn cọc cho booking #{booking.booking_id}.";
            return RedirectBack(returnUrl);
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
            catch { }
        }

        private ActionResult RedirectBack(string returnUrl)
        {
            var fallback = Url.Action("Bookings", "BusinessBookings");
            return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? fallback : returnUrl);
        }

        private decimal CalcDeposit(Bookings b)
        {
            if (b == null) return 0m;
            int guests = b.number_of_guests <= 0 ? 0 : b.number_of_guests;
            return guests * DEPOSIT_PER_GUEST;
        }

        private decimal GetDepositNet(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return 0m;

            decimal paid = payments
                .Where(p => p != null
                            && IsKind(p.payment_kind, KIND_DEPOSIT)
                            && p.amount > 0m
                            && IsPaidPaymentStatus(p.status))
                .Sum(p => p.amount);

            decimal refundedAbs = payments
                .Where(p => p != null
                            && IsKind(p.payment_kind, KIND_REFUND_DEPOSIT)
                            && p.amount < 0m
                            && IsRefundPaymentStatus(p.status))
                .Sum(p => Math.Abs(p.amount));

            return Math.Max(0m, paid - refundedAbs);
        }

        private Payments GetLatestPaidDeposit(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return null;

            return payments
                .Where(p => p != null
                            && IsKind(p.payment_kind, KIND_DEPOSIT)
                            && p.amount > 0m
                            && p.paid_at.HasValue
                            && IsPaidPaymentStatus(p.status))
                .OrderByDescending(p => p.paid_at.Value)
                .ThenByDescending(p => p.payment_id)
                .FirstOrDefault();
        }

        private Payments GetLatestRefundDeposit(ICollection<Payments> payments)
        {
            if (payments == null || payments.Count == 0) return null;

            return payments
                .Where(p => p != null
                            && IsKind(p.payment_kind, KIND_REFUND_DEPOSIT)
                            && p.paid_at.HasValue
                            && IsRefundPaymentStatus(p.status))
                .OrderByDescending(p => p.paid_at.Value)
                .ThenByDescending(p => p.payment_id)
                .FirstOrDefault();
        }

        private bool IsRefundAfterPaid(Payments paidDeposit, Payments refundedDeposit)
        {
            if (paidDeposit == null || refundedDeposit == null) return false;
            if (!paidDeposit.paid_at.HasValue || !refundedDeposit.paid_at.HasValue) return false;
            return refundedDeposit.paid_at.Value >= paidDeposit.paid_at.Value;
        }

        private bool IsKind(string kind, string expected)
        {
            if (string.IsNullOrWhiteSpace(kind)) return false;
            return kind.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPaidPaymentStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
                || status.IndexOf("thành công", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("đã cọc", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsRefundPaymentStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim();

            return status.Equals("REFUNDED", StringComparison.OrdinalIgnoreCase)
                || status.IndexOf("hoàn", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("reversed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCanceledBookingStatus(string bookingStatus)
        {
            if (string.IsNullOrWhiteSpace(bookingStatus)) return false;
            bookingStatus = bookingStatus.Trim();

            return bookingStatus.IndexOf("hủy", StringComparison.OrdinalIgnoreCase) >= 0
                || bookingStatus.IndexOf("huỷ", StringComparison.OrdinalIgnoreCase) >= 0
                || bookingStatus.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string NormalizeReturnUrl(string returnUrl)
        {
            var fallback = Url.Action("Bookings", "BusinessBookings");
            if (string.IsNullOrWhiteSpace(returnUrl)) return fallback;
            if (!Url.IsLocalUrl(returnUrl)) return fallback;
            return returnUrl;
        }

        private bool IsBusiness()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString().Trim();
            return role.Equals("Business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("DoanhNghiep", StringComparison.OrdinalIgnoreCase)
                || role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private int? GetCurrentUserId()
        {
            var u = Session["user"] as Users;
            if (u != null && u.user_id > 0) return u.user_id;

            var raw = Session["user_id"] ?? Session["UserId"] ?? Session["USER_ID"];
            if (raw == null) return null;

            int id;
            return int.TryParse(raw.ToString(), out id) && id > 0 ? id : (int?)null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
