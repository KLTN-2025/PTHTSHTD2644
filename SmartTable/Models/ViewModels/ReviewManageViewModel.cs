using System;
using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class ReviewManageViewModel
    {
        public int ReviewId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserPhone { get; set; }
        public byte Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }

        public int ReportCount { get; set; }
        public string ReplyText { get; set; }
        public DateTime? ReplyCreatedAt { get; set; }
        public bool IsReported { get; set; }
    }

    public class ReviewReportViewModel
    {
        public int ReportId { get; set; }
        public int ReviewId { get; set; }
        public string ReviewComment { get; set; }
        public byte ReviewRating { get; set; }
        public string UserName { get; set; }
        public string ReportType { get; set; } 
        public string ReportReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } 
        public int ReportCount { get; set; } 
    }

    public class ReviewReplyViewModel
    {
        public int ReviewId { get; set; }
        public string ReplyText { get; set; }
        public int RestaurantId { get; set; }
    }

    public class ReviewMetricsViewModel
    {
        public int TotalReviews { get; set; }
        public double AverageRating { get; set; }
        public int Count1Star { get; set; }
        public int Count2Star { get; set; }
        public int Count3Star { get; set; }
        public int Count4Star { get; set; }
        public int Count5Star { get; set; }

        public double FoodQualityRating { get; set; }
        public double ServiceRating { get; set; }
        public double AmbienceRating { get; set; }
        public double ValueForMoneyRating { get; set; }

        public int PendingReportCount { get; set; }
        public int RecentReportsCount { get; set; } 
        public int RepliedReviewsCount { get; set; }
        public decimal ReplyRate => TotalReviews > 0 ? (decimal)RepliedReviewsCount / TotalReviews * 100 : 0;
    }
}