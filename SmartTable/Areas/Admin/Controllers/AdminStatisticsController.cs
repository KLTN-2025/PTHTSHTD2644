using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Areas.Admin.Controllers
{
    public class AdminStatisticsController : Controller
    {
        private readonly Entities db = new Entities();
        private const decimal DEPOSIT_PER_GUEST = 50000m;
        private const string KIND_DEPOSIT = "deposit";
        private const string KIND_PREORDER = "preorder";
        private const string KIND_FULL = "full";

        private const string KIND_REFUND_DEPOSIT = "refund_deposit";
        private const string KIND_REFUND_PREORDER = "refund_preorder";
        private const string KIND_REFUND_FULL = "refund_full";

        [HttpGet]
        public ActionResult Index(DateTime? from, DateTime? to, int? restaurantId = null)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home", new { area = "" });

            var fromDate = (from ?? DateTime.Today.AddDays(-180)).Date;
            var toDate = (to ?? DateTime.Today).Date;
            if (fromDate > toDate) { var t = fromDate; fromDate = toDate; toDate = t; }
            var toExclusive = toDate.AddDays(1);

            ViewBag.Restaurants = db.Restaurants.AsNoTracking()
                .OrderBy(r => r.name)
                .Select(r => new SelectListItem
                {
                    Value = r.restaurant_id.ToString(),
                    Text = r.name,
                    Selected = restaurantId.HasValue && r.restaurant_id == restaurantId.Value
                })
                .ToList();

            var totalBusinesses = db.Users.AsNoTracking()
                .Count(u => u.role != null && u.role == "Business");

            var totalRestaurants = db.Restaurants.AsNoTracking().Count();

            var payQuery = db.Payments.AsNoTracking()
                .Where(p => p.booking_id.HasValue
                            && p.paid_at.HasValue
                            && p.paid_at.Value >= fromDate
                            && p.paid_at.Value < toExclusive
                            && p.amount != 0m);

            if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                payQuery =
                    from p in payQuery
                    join b in db.Bookings.AsNoTracking() on p.booking_id equals (int?)b.booking_id
                    where b.restaurant_id.HasValue && b.restaurant_id.Value == restaurantId.Value
                    select p;
            }

            var pays = payQuery.ToList();
            if (pays.Count == 0)
            {
                var emptyVm = new AdminStatisticsViewModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    RestaurantId = restaurantId,

                    TotalBusinesses = totalBusinesses,
                    TotalRestaurants = totalRestaurants,

                    TotalBookingsInRange = 0,
                    PaidBookingsInRange = 0,
                    CanceledBookingsInRange = 0,

                    TotalDeposit = 0m,
                    TotalPreOrder = 0m,

                    DailyStats = new List<DailyStatItem>(),
                    HourlyStats = new List<HourlyStatItem>(),
                    RestaurantStats = new List<RestaurantStatItem>(),
                    TopMenuItems = new List<MenuItemStatItem>(),
                    TopCustomers = new List<CustomerStatItem>(),
                    PeakHour = new PeakHourSummary()
                };
                return View("Index", emptyVm);
            }

            var bookingIds = pays.Select(x => x.booking_id.Value).Distinct().ToList();

            var bookings = db.Bookings.AsNoTracking()
                .Include("Restaurants")
                .Include("Users")
                .Where(b => bookingIds.Contains(b.booking_id))
                .ToList();

            var bookingMap = bookings.ToDictionary(b => b.booking_id, b => b);

            Func<string, bool> isCanceledStatus = (s) =>
            {
                s = (s ?? "").Trim();
                if (s.Length == 0) return false;
                return s.IndexOf("Hủy", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0;
            };

            var canceledBookingIds = new HashSet<int>(
                bookings.Where(b => isCanceledStatus(b.status)).Select(b => b.booking_id)
            );

            var preorderValueDict = GetPreOrderTotalsByBookingIds(bookingIds);

            var rows = new List<BookingStatRow>();

            foreach (var bid in bookingIds)
            {
                if (!bookingMap.ContainsKey(bid)) continue;

                var b = bookingMap[bid];

                var depositExpected = CalculateDeposit(b);
                preorderValueDict.TryGetValue(bid, out var preorderValue);

                var paymentsOfBooking = pays.Where(p => p.booking_id.HasValue && p.booking_id.Value == bid).ToList();

                var paidAtAnchor = paymentsOfBooking
                    .Where(p => p.paid_at.HasValue)
                    .Select(p => p.paid_at.Value)
                    .OrderBy(x => x)
                    .FirstOrDefault();

                var paySum = SummarizePaymentsByKindNormalized(paymentsOfBooking, depositExpected, preorderValue);

                rows.Add(new BookingStatRow
                {
                    Booking = b,
                    PaidAtAnchor = paidAtAnchor,

                    IsCanceled = canceledBookingIds.Contains(bid),

                    DepositExpected = depositExpected,
                    PreOrderValue = preorderValue,

                    DepositReceived = paySum.DepositReceived,
                    PreOrderReceived = paySum.PreOrderReceived,
                    TotalReceived = paySum.DepositReceived + paySum.PreOrderReceived,

                    PaidSum = paySum.PaidSum,
                    RefundSum = paySum.RefundSum,
                    NetSum = paySum.NetSum,
                    HasRefund = paySum.HasRefund
                });
            }

            var paidRows = rows.Where(x => !x.IsCanceled && x.TotalReceived > 0m).ToList();

            var totalBookingsInRange = rows.Count;                 
            var canceledBookingsInRange = paidRows.Count == 0
                ? 0
                : paidRows.Count(x => x.IsCanceled);              

            var paidBookingsInRange = paidRows.Count;

            var totalDeposit = paidRows.Sum(x => x.DepositReceived);
            var totalPreOrder = paidRows.Sum(x => x.PreOrderReceived);

            var daily = paidRows
                .GroupBy(x => x.PaidAtAnchor.Date)
                .Select(g => new DailyStatItem
                {
                    Date = g.Key,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(x => x.DepositReceived),
                    PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                })
                .OrderBy(x => x.Date)
                .ToList();

            var hourly = paidRows
                .GroupBy(x => x.PaidAtAnchor.Hour)
                .Select(g => new HourlyStatItem
                {
                    Hour = g.Key,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(x => x.DepositReceived),
                    PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                })
                .OrderBy(x => x.Hour)
                .ToList();

            var peak = new PeakHourSummary();
            var best = hourly
                .OrderByDescending(x => x.BookingCount)
                .ThenByDescending(x => x.TotalAmount)
                .FirstOrDefault();

            if (best != null)
            {
                peak.BestHour = best.Hour;
                peak.BestHourBookings = best.BookingCount;
                peak.BestHourRevenue = best.TotalAmount;
            }

            var restaurantStats = paidRows
                .GroupBy(x => new
                {
                    Id = x.Booking.restaurant_id ?? 0,
                    Name = x.Booking.Restaurants != null ? x.Booking.Restaurants.name : "Nhà hàng"
                })
                .Select(g => new RestaurantStatItem
                {
                    RestaurantId = g.Key.Id,
                    RestaurantName = g.Key.Name,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(x => x.DepositReceived),
                    PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToList();

            var paidPreorderBookingIds = new HashSet<int>(
                paidRows.Where(x => x.PreOrderReceived > 0m).Select(x => x.Booking.booking_id)
            );
            var topMenu = GetTopMenuItemsFromPaidSet(paidPreorderBookingIds);

            var topCustomers = paidRows
                .Where(x => x.Booking.user_id.HasValue)
                .GroupBy(x => x.Booking.user_id.Value)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.PaidAtAnchor).FirstOrDefault();
                    var u = last != null ? last.Booking.Users : null;

                    return new CustomerStatItem
                    {
                        UserId = g.Key,
                        CustomerName = (u != null && !string.IsNullOrWhiteSpace(u.full_name)) ? u.full_name : "Khách",
                        Phone = u != null ? u.phone : null,

                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived),
                        LastBookingTime = last != null ? (DateTime?)last.PaidAtAnchor : null
                    };
                })
                .OrderByDescending(x => x.TotalAmount)
                .ThenByDescending(x => x.BookingCount)
                .Take(20)
                .ToList();

            var vm = new AdminStatisticsViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                RestaurantId = restaurantId,

                TotalBusinesses = totalBusinesses,
                TotalRestaurants = totalRestaurants,

                TotalBookingsInRange = totalBookingsInRange,
                PaidBookingsInRange = paidBookingsInRange,
                CanceledBookingsInRange = canceledBookingIds.Count, 

                TotalDeposit = totalDeposit,
                TotalPreOrder = totalPreOrder,

                DailyStats = daily,
                HourlyStats = hourly,
                RestaurantStats = restaurantStats,
                TopMenuItems = topMenu,
                TopCustomers = topCustomers,
                PeakHour = peak
            };

            return View("Index", vm);
        }

        private class PaymentSummary
        {
            public decimal PaidSum { get; set; }
            public decimal RefundSum { get; set; }
            public decimal NetSum { get; set; }
            public bool HasRefund { get; set; }

            public decimal DepositReceived { get; set; }
            public decimal PreOrderReceived { get; set; }
        }

        private PaymentSummary SummarizePaymentsByKindNormalized(List<Payments> payments, decimal depositExpected, decimal preorderValue)
        {
            decimal depositPaid = 0m, preorderPaid = 0m, fullPaid = 0m;
            decimal refundDeposit = 0m, refundPre = 0m, refundFull = 0m;

            bool hasRefund = false;

            if (payments != null && payments.Count > 0)
            {
                foreach (var p in payments)
                {
                    if (p == null) continue;
                    if (!p.paid_at.HasValue) continue;

                    var kind = NormalizeKind(p.payment_kind, p.status, p.amount);
                    var amt = p.amount; // dương/âm

                    if (IsRefundKind(kind, amt, p.status))
                    {
                        hasRefund = true;
                        var abs = Math.Abs(amt);

                        if (kind == KIND_REFUND_DEPOSIT) refundDeposit += abs;
                        else if (kind == KIND_REFUND_PREORDER) refundPre += abs;
                        else if (kind == KIND_REFUND_FULL) refundFull += abs;
                        else
                        {
                            refundDeposit += abs;
                        }
                        continue;
                    }

                    if (amt <= 0m) continue;

                    if (kind == KIND_DEPOSIT) depositPaid += amt;
                    else if (kind == KIND_PREORDER) preorderPaid += amt;
                    else if (kind == KIND_FULL) fullPaid += amt;
                    else
                    {
                        fullPaid += amt;
                    }
                }
            }

            var depNet = Math.Max(0m, depositPaid - refundDeposit);
            var preNet = Math.Max(0m, preorderPaid - refundPre);
            var fullNet = Math.Max(0m, fullPaid - refundFull);

            var paidSum = depositPaid + preorderPaid + fullPaid;
            var refundSum = refundDeposit + refundPre + refundFull;
            var netSum = Math.Max(0m, paidSum - refundSum);

            var depositReceived = Math.Min(depositExpected, depNet);
            var depGap = Math.Max(0m, depositExpected - depositReceived);

            var useFullToDeposit = Math.Min(depGap, fullNet);
            depositReceived += useFullToDeposit;

            var remainingFull = Math.Max(0m, fullNet - useFullToDeposit);

            var preorderReceived = Math.Min(preorderValue, preNet);
            var preGap = Math.Max(0m, preorderValue - preorderReceived);

            var useFullToPre = Math.Min(preGap, remainingFull);
            preorderReceived += useFullToPre;

            return new PaymentSummary
            {
                PaidSum = paidSum,
                RefundSum = refundSum,
                NetSum = netSum,
                HasRefund = hasRefund,
                DepositReceived = depositReceived,
                PreOrderReceived = preorderReceived
            };
        }

        private string NormalizeKind(string paymentKind, string status, decimal amount)
        {
            var kind = (paymentKind ?? "").Trim().ToLowerInvariant();
            var st = (status ?? "").Trim();

            if (kind == "deposit" || kind.Contains("cọc")) return KIND_DEPOSIT;
            if (kind == "preorder" || kind.Contains("món")) return KIND_PREORDER;
            if (kind == "full" || kind.Contains("tổng")) return KIND_FULL;

            if (kind == "refund_deposit" || kind == "refunddeposit" || kind.Contains("hoàn cọc")) return KIND_REFUND_DEPOSIT;
            if (kind == "refund_preorder" || kind == "refundpreorder" || kind.Contains("hoàn món")) return KIND_REFUND_PREORDER;
            if (kind == "refund_full" || kind == "refundfull" || kind.Contains("hoàn tổng")) return KIND_REFUND_FULL;

            if (amount < 0m)
            {
                if (st.IndexOf("món", StringComparison.OrdinalIgnoreCase) >= 0) return KIND_REFUND_PREORDER;
                return KIND_REFUND_DEPOSIT;
            }

            if (st.IndexOf("món", StringComparison.OrdinalIgnoreCase) >= 0) return KIND_PREORDER;

            return KIND_DEPOSIT;
        }

        private bool IsRefundKind(string kind, decimal amount, string status)
        {
            if (kind == KIND_REFUND_DEPOSIT || kind == KIND_REFUND_PREORDER || kind == KIND_REFUND_FULL) return true;
            if (amount < 0m) return true;

            var st = (status ?? "").Trim();
            return st.IndexOf("hoàn", StringComparison.OrdinalIgnoreCase) >= 0
                || st.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Dictionary<int, decimal> GetPreOrderTotalsByBookingIds(List<int> bookingIds)
        {
            if (bookingIds == null || bookingIds.Count == 0)
                return new Dictionary<int, decimal>();

            var rows = (from o in db.Orders.AsNoTracking()
                        join m in db.MenuItems.AsNoTracking() on o.menu_item_id equals m.menu_item_id
                        where o.booking_id.HasValue && bookingIds.Contains(o.booking_id.Value)
                        group new { o, m } by o.booking_id.Value into g
                        select new
                        {
                            BookingId = g.Key,
                            Total = g.Sum(x => (decimal?)(((int?)x.o.quantity ?? 0) * ((decimal?)x.m.price ?? 0m))) ?? 0m
                        }).ToList();

            return rows.ToDictionary(x => x.BookingId, x => x.Total);
        }

        private List<MenuItemStatItem> GetTopMenuItemsFromPaidSet(HashSet<int> paidBookingIdSet)
        {
            if (paidBookingIdSet == null || paidBookingIdSet.Count == 0)
                return new List<MenuItemStatItem>();

            var rows = (from o in db.Orders.AsNoTracking()
                        where o.booking_id.HasValue && paidBookingIdSet.Contains(o.booking_id.Value)
                        join m in db.MenuItems.AsNoTracking() on o.menu_item_id equals m.menu_item_id
                        select new
                        {
                            MenuItemId = m.menu_item_id,
                            Name = m.name,
                            Qty = (int?)o.quantity ?? 0,
                            Price = (decimal?)m.price ?? 0m
                        }).ToList();

            return rows
                .GroupBy(x => new { x.MenuItemId, x.Name })
                .Select(g => new MenuItemStatItem
                {
                    MenuItemId = g.Key.MenuItemId,
                    MenuItemName = g.Key.Name,
                    Quantity = g.Sum(x => x.Qty),
                    Revenue = g.Sum(x => x.Qty * x.Price)
                })
                .OrderByDescending(x => x.Revenue)
                .ThenByDescending(x => x.Quantity)
                .Take(20)
                .ToList();
        }

        private decimal CalculateDeposit(Bookings b)
        {
            if (b == null) return 0m;
            if (b.number_of_guests <= 0) return 0m;
            return b.number_of_guests * DEPOSIT_PER_GUEST;
        }

        private bool IsAdmin()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString();
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private class BookingStatRow
        {
            public Bookings Booking { get; set; }
            public DateTime PaidAtAnchor { get; set; }
            public bool IsCanceled { get; set; }

            public decimal DepositExpected { get; set; }
            public decimal PreOrderValue { get; set; }

            public decimal PaidSum { get; set; }
            public decimal RefundSum { get; set; }
            public decimal NetSum { get; set; }
            public bool HasRefund { get; set; }

            public decimal DepositReceived { get; set; }
            public decimal PreOrderReceived { get; set; }
            public decimal TotalReceived { get; set; }
        }
    }
}
