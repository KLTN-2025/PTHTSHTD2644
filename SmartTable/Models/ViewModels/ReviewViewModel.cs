using System;
using System.ComponentModel.DataAnnotations;

namespace SmartTable.Models.ViewModels
{
 
 


    public class ReviewCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn mức đánh giá (1-5 sao).")]
        [Range(1, 5, ErrorMessage = "Mức đánh giá phải từ 1 đến 5.")]
        public byte Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập bình luận.")]
        [MinLength(10, ErrorMessage = "Bình luận tối thiểu 10 ký tự.")]
        [MaxLength(500, ErrorMessage = "Bình luận tối đa 500 ký tự.")]
        public string Comment { get; set; }

        public int RestaurantId { get; set; }
        public int? BookingId { get; set; } 
    }


    public class RatingStatisticsViewModel
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        
        public int Count5Star { get; set; }
        public int Count4Star { get; set; }
        public int Count3Star { get; set; }
        public int Count2Star { get; set; }
        public int Count1Star { get; set; }
        
        public decimal Percentage5Star => TotalReviews > 0 ? (decimal)Count5Star / TotalReviews * 100 : 0;
        public decimal Percentage4Star => TotalReviews > 0 ? (decimal)Count4Star / TotalReviews * 100 : 0;
        public decimal Percentage3Star => TotalReviews > 0 ? (decimal)Count3Star / TotalReviews * 100 : 0;
        public decimal Percentage2Star => TotalReviews > 0 ? (decimal)Count2Star / TotalReviews * 100 : 0;
        public decimal Percentage1Star => TotalReviews > 0 ? (decimal)Count1Star / TotalReviews * 100 : 0;
    }
}