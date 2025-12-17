using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Controllers
{
    public class BusinessStatisticsController : Controller
    {
        private readonly Entities db = new Entities();

        private const decimal DEPOSIT_PER_GUEST = 50000m;

        // ===== Payment kinds =====
        // Thu từ khách
        private const string KIND_DEPOSIT = "deposit";     
        private const string KIND_PREORDER = "preorder";   
        private const string KIND_FULL = "full";           

        // Hoàn cho khách
        private const string KIND_REFUND_DEPOSIT = "refund_deposit";
        private const string KIND_REFUND_PREORDER = "refund_preorder";
        private const string KIND_REFUND_FULL = "refund_full";

        // ================== INDEX ==================
        [HttpGet]
        public ActionResult Index(DateTime? from, DateTime? to, int? restaurantId = null)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
            var toDate = (to ?? DateTime.Today).Date;
            if (fromDate > toDate) { var t = fromDate; fromDate = toDate; toDate = t; }
            var toExclusive = toDate.AddDays(1);

            ViewBag.Restaurants = db.Restaurants.AsNoTracking()
                .Where(r => r.user_id == userId.Value)
                .OrderBy(r => r.name)
                .Select(r => new SelectListItem
                {
                    Value = r.restaurant_id.ToString(),
                    Text = r.name,
                    Selected = restaurantId.HasValue && r.restaurant_id == restaurantId.Value
                })
                .ToList();

            ViewBag.SelectedRestaurantId = restaurantId;
            ViewBag.From = fromDate;
            ViewBag.To = toDate;

            var bookingsQuery = db.Bookings
                .AsNoTracking()
                .Include("Restaurants")
                .Include("Users")
                .Include("Payments")
                .Where(b =>
                    b.Restaurants != null &&
                    b.Restaurants.user_id == userId.Value &&
                    b.booking_time >= fromDate &&
                    b.booking_time < toExclusive);

            if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                bookingsQuery = bookingsQuery.Where(b => b.restaurant_id.HasValue && b.restaurant_id.Value == restaurantId.Value);
            }

            var bookings = bookingsQuery.ToList();
            var bookingIds = bookings.Select(x => x.booking_id).ToList();

            var preorderByBooking = GetPreOrderTotalsByBookingIds(bookingIds);

            var rows = new List<BookingStatRow>();

            foreach (var b in bookings)
            {
                var depositExpected = CalculateDeposit(b);

                decimal preorderTotal = 0m;
                if (preorderByBooking.TryGetValue(b.booking_id, out var po))
                    preorderTotal = po;

                var pay = SummarizePaymentsByKind(b.Payments, depositExpected, preorderTotal);

                rows.Add(new BookingStatRow
                {
                    Booking = b,

                    DepositExpected = depositExpected,
                    PreOrderTotal = preorderTotal,

                    DepositReceived = pay.DepositReceived,
                    PreOrderReceived = pay.PreOrderReceived,
                    TotalReceived = pay.DepositReceived + pay.PreOrderReceived,

                    PaidSum = pay.PaidSum,
                    RefundSum = pay.RefundSum,
                    NetSum = pay.NetSum,
                    HasRefund = pay.HasRefund
                });
            }

            var vm = new StatisticsViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,

                TotalBookings = rows.Count,
                TotalDeposit = rows.Sum(x => x.DepositReceived),
                TotalPreOrderAmount = rows.Sum(x => x.PreOrderReceived),

                RestaurantStats = rows
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
                    .ToList(),

                DailyStats = rows
                    .GroupBy(x => x.Booking.booking_time.Date)
                    .Select(g => new DailyStatItem
                    {
                        Date = g.Key,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                    })
                    .OrderBy(x => x.Date)
                    .ToList(),

                HourlyStats = rows
                    .GroupBy(x => x.Booking.booking_time.Hour)
                    .Select(g => new HourlyStatItem
                    {
                        Hour = g.Key,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                    })
                    .OrderBy(x => x.Hour)
                    .ToList(),

                WeeklyStats = rows
                    .GroupBy(x => GetWeekStart(x.Booking.booking_time))
                    .Select(g => new WeeklyStatItem
                    {
                        Year = g.Key.Year,
                        Week = GetIsoWeek(g.Key),
                        WeekStart = g.Key,
                        WeekEnd = g.Key.AddDays(6),
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                    })
                    .OrderByDescending(x => x.WeekStart)
                    .ToList(),

                MonthlyStats = rows
                    .GroupBy(x => new { x.Booking.booking_time.Year, x.Booking.booking_time.Month })
                    .Select(g => new MonthlyStatItem
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                    })
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .ToList(),

                YearlyStats = rows
                    .GroupBy(x => x.Booking.booking_time.Year)
                    .Select(g => new YearlyStatItem
                    {
                        Year = g.Key,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived)
                    })
                    .OrderByDescending(x => x.Year)
                    .ToList(),

                PeakHour = BuildPeakHour(rows),

                TopMenuItems = GetTopMenuItemsFromPaidSet(new HashSet<int>(
                    rows.Where(x => x.PreOrderReceived > 0m).Select(x => x.Booking.booking_id)
                )),

                TopCustomers = GetTopCustomers(rows)
            };

            return View("~/Views/BusinessStatistics/Index.cshtml", vm);
        }

        // ================== PAYMENT SUMMARY (CHUẨN) ==================
        private class PaymentSummary
        {
            public decimal PaidSum { get; set; }     
            public decimal RefundSum { get; set; }   
            public decimal NetSum { get; set; }      
            public bool HasRefund { get; set; }

            public decimal DepositReceived { get; set; }
            public decimal PreOrderReceived { get; set; }
        }

   
        private PaymentSummary SummarizePaymentsByKind(ICollection<Payments> payments, decimal depositExpected, decimal preorderTotal)
        {
            decimal depositPaid = 0m, preorderPaid = 0m, fullPaid = 0m;
            decimal refundDeposit = 0m, refundPre = 0m, refundFull = 0m;

            bool hasRefund = false;

            if (payments != null && payments.Count > 0)
            {
                foreach (var p in payments)
                {
                    if (p == null) continue;

                    var paidAt = p.paid_at;
                    if (!paidAt.HasValue) continue;

                    var kind = (p.payment_kind ?? "").Trim().ToLowerInvariant();
                    var amt = p.amount; 

                    if (kind == KIND_DEPOSIT)
                    {
                        if (amt > 0) depositPaid += amt;
                        else { hasRefund = true; refundDeposit += Math.Abs(amt); }
                        continue;
                    }

                    if (kind == KIND_PREORDER)
                    {
                        if (amt > 0) preorderPaid += amt;
                        else { hasRefund = true; refundPre += Math.Abs(amt); }
                        continue;
                    }

                    if (kind == KIND_FULL)
                    {
                        if (amt > 0) fullPaid += amt;
                        else { hasRefund = true; refundFull += Math.Abs(amt); }
                        continue;
                    }

                    if (kind == KIND_REFUND_DEPOSIT)
                    {
                        hasRefund = true;
                        refundDeposit += Math.Abs(amt);
                        continue;
                    }

                    if (kind == KIND_REFUND_PREORDER)
                    {
                        hasRefund = true;
                        refundPre += Math.Abs(amt);
                        continue;
                    }

                    if (kind == KIND_REFUND_FULL)
                    {
                        hasRefund = true;
                        refundFull += Math.Abs(amt);
                        continue;
                    }

                 
                    if (amt > 0) fullPaid += amt;
                    else { hasRefund = true; refundFull += Math.Abs(amt); }
                }
            }

            // Net theo kind
            var depNet = Math.Max(0m, depositPaid - refundDeposit);
            var preNet = Math.Max(0m, preorderPaid - refundPre);
            var fullNet = Math.Max(0m, fullPaid - refundFull);

            // Paid/Refund tổng
            var paidSum = depositPaid + preorderPaid + fullPaid;
            var refundSum = refundDeposit + refundPre + refundFull;
            var netSum = Math.Max(0m, paidSum - refundSum);

            // ===== phân bổ FULL vào deposit trước, rồi preorder =====
            var depositReceived = Math.Min(depositExpected, depNet);
            var depGap = Math.Max(0m, depositExpected - depositReceived);

            var useFullToDeposit = Math.Min(depGap, fullNet);
            depositReceived += useFullToDeposit;

            var remainingFull = Math.Max(0m, fullNet - useFullToDeposit);

            var preorderReceived = Math.Min(preorderTotal, preNet);
            var preGap = Math.Max(0m, preorderTotal - preorderReceived);

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

        // ================== TOP MENU ==================
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

        // ================== TOP CUSTOMERS ==================
        private List<CustomerStatItem> GetTopCustomers(List<BookingStatRow> rows)
        {
            if (rows == null || rows.Count == 0) return new List<CustomerStatItem>();

            return rows
                .Where(x => x.Booking.user_id.HasValue)
                .GroupBy(x => x.Booking.user_id.Value)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.Booking.booking_time).FirstOrDefault();
                    var u = last != null ? last.Booking.Users : null;

                    return new CustomerStatItem
                    {
                        UserId = g.Key,
                        CustomerName = (u != null && !string.IsNullOrWhiteSpace(u.full_name)) ? u.full_name : "Khách",
                        Phone = u != null ? u.phone : null,

                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(x => x.DepositReceived),
                        PreOrderAmount = g.Sum(x => x.PreOrderReceived),
                        LastBookingTime = last != null ? (DateTime?)last.Booking.booking_time : null
                    };
                })
                .OrderByDescending(x => x.TotalAmount)
                .ThenByDescending(x => x.BookingCount)
                .Take(20)
                .ToList();
        }

        // ================== PEAK HOUR ==================
        private PeakHourSummary BuildPeakHour(List<BookingStatRow> rows)
        {
            var res = new PeakHourSummary();
            if (rows == null || rows.Count == 0) return res;

            var best = rows
                .GroupBy(x => x.Booking.booking_time.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Bookings = g.Count(),
                    Revenue = g.Sum(x => x.TotalReceived)
                })
                .OrderByDescending(x => x.Bookings)
                .ThenByDescending(x => x.Revenue)
                .FirstOrDefault();

            if (best != null)
            {
                res.BestHour = best.Hour;
                res.BestHourBookings = best.Bookings;
                res.BestHourRevenue = best.Revenue;
            }

            return res;
        }

        // ================== PREORDER TOTALS (ORDERS) ==================
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

        // ================== DEPOSIT EXPECTED ==================
        private decimal CalculateDeposit(Bookings b)
        {
            if (b == null) return 0m;
            if (b.number_of_guests <= 0) return 0m;
            return b.number_of_guests * DEPOSIT_PER_GUEST;
        }

        // ================== WEEK HELPERS ==================
        private DateTime GetWeekStart(DateTime date)
        {
            var d = date.Date;
            int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return d.AddDays(-diff);
        }

        private int GetIsoWeek(DateTime date)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            var day = cal.GetDayOfWeek(date);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
                date = date.AddDays(3);

            return cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        // ================== AUTH HELPERS ==================
        private bool IsBusiness()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString();
            if (string.IsNullOrWhiteSpace(role)) return false;

            return role.Equals("Business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("DoanhNghiep", StringComparison.OrdinalIgnoreCase)
                || role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase);
        }

        private int? GetCurrentUserId()
        {
            var raw = Session["user_id"] ?? Session["UserId"] ?? Session["USER_ID"];
            if (raw == null) return null;

            int id;
            if (int.TryParse(raw.ToString(), out id) && id > 0) return id;
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ================== INTERNAL DTO ==================
        private class BookingStatRow
        {
            public Bookings Booking { get; set; }

            public decimal DepositExpected { get; set; }
            public decimal PreOrderTotal { get; set; }

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
