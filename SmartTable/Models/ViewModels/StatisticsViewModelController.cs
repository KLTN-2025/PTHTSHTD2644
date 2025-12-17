using System;
using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class StatisticsViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public int TotalBookings { get; set; }

        public decimal TotalDeposit { get; set; }
        public decimal TotalPreOrderAmount { get; set; }
        public decimal TotalAmount => TotalDeposit + TotalPreOrderAmount;


        public decimal RevenueDeposit => TotalDeposit;
        public decimal RevenuePreOrder => TotalPreOrderAmount;
        public decimal RevenueTotal => TotalAmount;

        public List<RestaurantStatItem> RestaurantStats { get; set; } = new List<RestaurantStatItem>();
        public List<DailyStatItem> DailyStats { get; set; } = new List<DailyStatItem>();

        public List<HourlyStatItem> HourlyStats { get; set; } = new List<HourlyStatItem>();
        public List<MenuItemStatItem> TopMenuItems { get; set; } = new List<MenuItemStatItem>();
        public List<CustomerStatItem> TopCustomers { get; set; } = new List<CustomerStatItem>();

        public List<WeeklyStatItem> WeeklyStats { get; set; } = new List<WeeklyStatItem>();
        public List<MonthlyStatItem> MonthlyStats { get; set; } = new List<MonthlyStatItem>();
        public List<YearlyStatItem> YearlyStats { get; set; } = new List<YearlyStatItem>();

        public PeakHourSummary PeakHour { get; set; } = new PeakHourSummary();
    }

    public class RestaurantStatItem
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;
    }

    public class DailyStatItem
    {
        public DateTime Date { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;
    }

    public class HourlyStatItem
    {
        public int Hour { get; set; } // 0..23

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;

        public string Label => $"{Hour:00}:00";
    }

    public class MenuItemStatItem
    {
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }

        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CustomerStatItem
    {
        public int UserId { get; set; }
        public string CustomerName { get; set; }
        public string Phone { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;

        public DateTime? LastBookingTime { get; set; }
    }

    public class WeeklyStatItem
    {
        public int Year { get; set; }
        public int Week { get; set; }

        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;
    }

    public class MonthlyStatItem
    {
        public int Year { get; set; }
        public int Month { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;

        public string Label => $"{Month:00}/{Year}";
    }

    public class YearlyStatItem
    {
        public int Year { get; set; }

        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal PreOrderAmount { get; set; }
        public decimal TotalAmount => DepositAmount + PreOrderAmount;
    }

    public class PeakHourSummary
    {
        public int? BestHour { get; set; }
        public int BestHourBookings { get; set; }
        public decimal BestHourRevenue { get; set; }

        public string BestHourLabel => BestHour.HasValue ? $"{BestHour.Value:00}:00" : "—";
    }
}
