using System;

namespace SmartTable.Models.ViewModels
{
    public class ReviewDisplayViewModel
    {
        public int ReviewId { get; set; }
        public string UserName { get; set; }
        public string UserInitial { get; set; }
        public byte Rating { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public string ReplyText { get; set; }
        public DateTime? ReplyCreatedAt { get; set; }
        public int ReportCount { get; set; }
        public bool IsReported { get; set; }
    }

    public class SubmitReviewReportViewModel
    {
        public int ReviewId { get; set; }
        public string ReportType { get; set; }
        public string ReportReason { get; set; }
    }
}