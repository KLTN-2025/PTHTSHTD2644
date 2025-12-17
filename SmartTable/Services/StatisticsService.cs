using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;

namespace SmartTable.Services
{
    public class StatisticsService
    {
        private readonly Entities db;
        private const decimal DepositPerGuest = 50000m;

        public StatisticsService(Entities dbContext)
        {
            db = dbContext;
        }

        public StatisticsViewModel Build(
            int businessUserId,
            DateTime fromDate,
            DateTime toDate,
            int? restaurantId = null,
            int topMenu = 10,
            int topCustomers = 10)
        {
            var from = fromDate.Date;
            var toExclusive = toDate.Date.AddDays(1);

            var restaurantIds = db.Restaurants
                .Where(r => r.user_id == businessUserId)
                .Select(r => r.restaurant_id)
                .ToList();

            if (restaurantId.HasValue && restaurantId.Value > 0)
                restaurantIds = restaurantIds.Where(id => id == restaurantId.Value).ToList();

            if (restaurantIds == null || restaurantIds.Count == 0)
                return NewEmpty(from, toDate.Date);

            var bookings = db.Bookings
                .Where(b =>
                    b.restaurant_id.HasValue &&
                    restaurantIds.Contains(b.restaurant_id.Value) &&
                    b.booking_time >= from &&
                    b.booking_time < toExclusive)
                .Select(b => new
                {
                    b.booking_id,
                    RestaurantId = b.restaurant_id.Value,
                    RestaurantName = (b.Restaurants != null ? b.Restaurants.name : "Nhà hàng"),
                    b.booking_time,
                    b.number_of_guests,
                    b.user_id,
                    CustomerName = (b.Users != null ? b.Users.full_name : null),
                    CustomerPhone = (b.Users != null ? b.Users.phone : null),
                    b.status,
                    b.special_request
                })
                .ToList();

            if (bookings.Count == 0)
                return NewEmpty(from, toDate.Date);

            var bookingIds = bookings.Select(x => x.booking_id).ToList();

 
            var preOrderByBooking = (from o in db.Orders
                                     join m in db.MenuItems on o.menu_item_id equals m.menu_item_id
                                     where o.booking_id.HasValue && bookingIds.Contains(o.booking_id.Value)
                                     select new
                                     {
                                         BookingId = o.booking_id.Value,
                                         Qty = (int?)o.quantity ?? 0,
                                         Price = (decimal?)m.price ?? 0m
                                     })
                                     .ToList()
                                     .GroupBy(x => x.BookingId)
                                     .ToDictionary(g => g.Key, g => g.Sum(t => t.Price * t.Qty));

            var legacyPreByBooking = new Dictionary<int, decimal>();
            foreach (var b in bookings)
            {
                ParseSpecialRequestLegacy(b.special_request ?? "", out _, out _, out var preTotalLegacy);
                legacyPreByBooking[b.booking_id] = preTotalLegacy;
            }

            var successPayments = db.Payments
                .Where(p =>
                    p.booking_id.HasValue &&
                    bookingIds.Contains(p.booking_id.Value) &&
                    p.status != null &&
                    p.status.Contains("Đã thanh toán"));

            var maxPayIds = successPayments
                .GroupBy(p => p.booking_id.Value)
                .Select(g => new { BookingId = g.Key, MaxPaymentId = g.Max(x => x.payment_id) })
                .ToList();

            var lastSuccessPaymentByBooking = (from p in db.Payments
                                               join mx in maxPayIds on p.payment_id equals mx.MaxPaymentId
                                               select new
                                               {
                                                   BookingId = mx.BookingId,
                                                   p.amount,
                                                   p.payment_method,
                                                   p.status,
                                                   p.payment_id
                                               })
                                              .ToList()
                                              .ToDictionary(x => x.BookingId, x => x);

            bool IsPaid(int bookingId) => lastSuccessPaymentByBooking.ContainsKey(bookingId);

            var paid = bookings
                .Where(x => IsPaid(x.booking_id))
                .Select(x =>
                {
                    var guests = x.number_of_guests <= 0 ? 1 : x.number_of_guests;
                    var deposit = guests * DepositPerGuest;

                    decimal pre = 0m;
                    if (preOrderByBooking.TryGetValue(x.booking_id, out var preOrders))
                        pre = preOrders;
                    else if (legacyPreByBooking.TryGetValue(x.booking_id, out var preLegacy))
                        pre = preLegacy;

                    return new PaidRow
                    {
                        BookingId = x.booking_id,
                        RestaurantId = x.RestaurantId,
                        RestaurantName = x.RestaurantName,
                        BookingTime = x.booking_time,
                        Guests = guests,
                        Deposit = deposit,
                        PreOrder = pre,
                        UserId = x.user_id ?? 0,
                        CustomerName = x.CustomerName,
                        CustomerPhone = x.CustomerPhone
                    };
                })
                .ToList();

            var vm = new StatisticsViewModel
            {
                FromDate = from,
                ToDate = toDate.Date,

                TotalBookings = bookings.Count,
                TotalDeposit = paid.Sum(x => x.Deposit),
                TotalPreOrderAmount = paid.Sum(x => x.PreOrder),

                RestaurantStats = paid
                    .GroupBy(x => new { x.RestaurantId, x.RestaurantName })
                    .OrderByDescending(g => g.Count())
                    .Select(g => new RestaurantStatItem
                    {
                        RestaurantId = g.Key.RestaurantId,
                        RestaurantName = g.Key.RestaurantName,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(t => t.Deposit),
                        PreOrderAmount = g.Sum(t => t.PreOrder)
                    })
                    .ToList(),

                DailyStats = paid
                    .GroupBy(x => x.BookingTime.Date)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new DailyStatItem
                    {
                        Date = g.Key,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(t => t.Deposit),
                        PreOrderAmount = g.Sum(t => t.PreOrder)
                    })
                    .ToList(),

                HourlyStats = paid
                    .GroupBy(x => x.BookingTime.Hour)
                    .OrderBy(g => g.Key)
                    .Select(g => new HourlyStatItem
                    {
                        Hour = g.Key,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(t => t.Deposit),
                        PreOrderAmount = g.Sum(t => t.PreOrder)
                    })
                    .ToList(),

                PeakHour = BuildPeakHour(paid),

                WeeklyStats = BuildWeekly(paid),
                MonthlyStats = BuildMonthly(paid),
                YearlyStats = BuildYearly(paid),

                TopMenuItems = BuildTopMenuItems(restaurantIds, from, toExclusive, topMenu),

                TopCustomers = paid
                    .GroupBy(x => new { x.UserId, x.CustomerName, x.CustomerPhone })
                    .OrderByDescending(g => g.Sum(t => (t.Deposit + t.PreOrder)))
                    .ThenByDescending(g => g.Count())
                    .Take(topCustomers)
                    .Select(g => new CustomerStatItem
                    {
                        UserId = g.Key.UserId,
                        CustomerName = string.IsNullOrWhiteSpace(g.Key.CustomerName) ? "Khách" : g.Key.CustomerName,
                        Phone = g.Key.CustomerPhone,
                        BookingCount = g.Count(),
                        DepositAmount = g.Sum(t => t.Deposit),
                        PreOrderAmount = g.Sum(t => t.PreOrder),
                        LastBookingTime = g.Max(t => (DateTime?)t.BookingTime)
                    })
                    .ToList()
            };

            return vm;
        }

        private PeakHourSummary BuildPeakHour(List<PaidRow> paid)
        {
            var best = paid
                .GroupBy(x => x.BookingTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Bookings = g.Count(),
                    Revenue = g.Sum(t => t.Deposit + t.PreOrder)
                })
                .OrderByDescending(x => x.Bookings)
                .ThenByDescending(x => x.Revenue)
                .FirstOrDefault();

            return new PeakHourSummary
            {
                BestHour = best?.Hour,
                BestHourBookings = best?.Bookings ?? 0,
                BestHourRevenue = best?.Revenue ?? 0m
            };
        }

        private List<MenuItemStatItem> BuildTopMenuItems(
    List<int> restaurantIds,
    DateTime fromDate,
    DateTime toExclusive,
    int top)
        {
            if (restaurantIds == null || restaurantIds.Count == 0)
                return new List<MenuItemStatItem>();

            var rows = (from o in db.Orders
                        where o.booking_id.HasValue
                        join b in db.Bookings on o.booking_id.Value equals b.booking_id
                        join m in db.MenuItems on o.menu_item_id equals m.menu_item_id
                        where b.restaurant_id.HasValue
                              && restaurantIds.Contains(b.restaurant_id.Value)
                              && b.booking_time >= fromDate
                              && b.booking_time < toExclusive
                        select new
                        {
                            MenuItemId = m.menu_item_id,
                            MenuName = m.name,
                            Qty = (int?)o.quantity ?? 0,
                            Price = (decimal?)m.price ?? 0m
                        })
                       .ToList();

            return rows
                .GroupBy(x => new { x.MenuItemId, x.MenuName })
                .Select(g => new MenuItemStatItem
                {
                    MenuItemId = g.Key.MenuItemId,
                    MenuItemName = g.Key.MenuName,
                    Quantity = g.Sum(t => t.Qty),
                    Revenue = g.Sum(t => t.Price * t.Qty)
                })
                .OrderByDescending(x => x.Revenue)
                .ThenByDescending(x => x.Quantity)
                .Take(top > 0 ? top : 10)
                .ToList();
        }


        private List<WeeklyStatItem> BuildWeekly(List<PaidRow> paid)
        {
            var cal = CultureInfo.InvariantCulture.Calendar;
            var rule = CalendarWeekRule.FirstFourDayWeek;
            var firstDay = DayOfWeek.Monday;

            return paid
                .GroupBy(x =>
                {
                    var week = cal.GetWeekOfYear(x.BookingTime, rule, firstDay);
                    var year = x.BookingTime.Year;
                    return new { year, week };
                })
                .OrderByDescending(g => g.Key.year).ThenByDescending(g => g.Key.week)
                .Select(g => new WeeklyStatItem
                {
                    Year = g.Key.year,
                    Week = g.Key.week,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(t => t.Deposit),
                    PreOrderAmount = g.Sum(t => t.PreOrder)
                })
                .ToList();
        }

        private List<MonthlyStatItem> BuildMonthly(List<PaidRow> paid)
        {
            return paid
                .GroupBy(x => new { x.BookingTime.Year, x.BookingTime.Month })
                .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthlyStatItem
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(t => t.Deposit),
                    PreOrderAmount = g.Sum(t => t.PreOrder)
                })
                .ToList();
        }

        private List<YearlyStatItem> BuildYearly(List<PaidRow> paid)
        {
            return paid
                .GroupBy(x => x.BookingTime.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => new YearlyStatItem
                {
                    Year = g.Key,
                    BookingCount = g.Count(),
                    DepositAmount = g.Sum(t => t.Deposit),
                    PreOrderAmount = g.Sum(t => t.PreOrder)
                })
                .ToList();
        }

        private StatisticsViewModel NewEmpty(DateTime from, DateTime to)
        {
            return new StatisticsViewModel
            {
                FromDate = from,
                ToDate = to,

                TotalBookings = 0,
                TotalDeposit = 0m,
                TotalPreOrderAmount = 0m,

                RestaurantStats = new List<RestaurantStatItem>(),
                DailyStats = new List<DailyStatItem>(),
                HourlyStats = new List<HourlyStatItem>(),
                WeeklyStats = new List<WeeklyStatItem>(),
                MonthlyStats = new List<MonthlyStatItem>(),
                YearlyStats = new List<YearlyStatItem>(),
                TopMenuItems = new List<MenuItemStatItem>(),
                TopCustomers = new List<CustomerStatItem>(),
                PeakHour = new PeakHourSummary()
            };
        }

        private void ParseSpecialRequestLegacy(string special, out string userNote, out string preOrderText, out decimal preOrderTotal)
        {
            userNote = null;
            preOrderText = null;
            preOrderTotal = 0m;

            if (string.IsNullOrWhiteSpace(special)) return;

            var keyPre = "Món chuẩn bị trước:";
            var keyTotal = "Tiền món ước tính:";

            int idxPre = special.IndexOf(keyPre, StringComparison.OrdinalIgnoreCase);
            int idxTotal = special.IndexOf(keyTotal, StringComparison.OrdinalIgnoreCase);

            if (idxPre > 0)
            {
                userNote = special.Substring(0, idxPre).Trim();
                if (userNote.EndsWith("|")) userNote = userNote.Substring(0, userNote.Length - 1).Trim();
            }
            else if (idxPre < 0)
            {
                userNote = special.Trim();
            }

            if (idxPre >= 0)
            {
                int start = idxPre + keyPre.Length;
                int end = special.Length;

                int pipe = special.IndexOf("|", start);
                if (pipe >= 0) end = pipe;

                if (idxTotal >= 0 && idxTotal > start && idxTotal < end)
                    end = idxTotal;

                preOrderText = special.Substring(start, end - start).Trim().Trim('|').Trim();
            }

            if (idxTotal >= 0)
            {
                int start = idxTotal + keyTotal.Length;
                int end = special.IndexOf("|", start);
                if (end < 0) end = special.Length;

                var totalStr = special.Substring(start, end - start).Trim();
                totalStr = totalStr.Replace("đ", "").Replace("Đ", "").Replace(".", "").Replace(",", "").Trim();

                if (decimal.TryParse(totalStr, out var parsed))
                    preOrderTotal = parsed;
            }
        }

        private class PaidRow
        {
            public int BookingId { get; set; }
            public int RestaurantId { get; set; }
            public string RestaurantName { get; set; }
            public DateTime BookingTime { get; set; }
            public int Guests { get; set; }
            public decimal Deposit { get; set; }
            public decimal PreOrder { get; set; }
            public int UserId { get; set; }
            public string CustomerName { get; set; }
            public string CustomerPhone { get; set; }
        }
    }
}
