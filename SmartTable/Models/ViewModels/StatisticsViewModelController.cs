using System.Collections.Generic;
using System;

namespace SmartTableSystem2.Models.ViewModels
{
    public class StatisticsViewModel
    {
        public int TotalBookings { get; set; }
        public decimal TotalDeposit { get; set; }
        public List<RestaurantStatistics> RestaurantStats { get; set; } = new List<RestaurantStatistics>();
    }

    public class RestaurantStatistics
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
    }

    public class DailyStatistics
    {
        public DateTime Date { get; set; }
        public int BookingCount { get; set; }
        public decimal DepositAmount { get; set; }
    }
}