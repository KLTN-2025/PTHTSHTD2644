using System;
using System.ComponentModel.DataAnnotations;

namespace SmartTable.Models.ViewModels
{
    public class BookingViewModel
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày giờ")]
        [Display(Name = "Thời gian đặt")]
        public DateTime BookingTime { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng khách")]
        [Range(1, 50, ErrorMessage = "Số lượng khách từ 1 đến 50")]
        [Display(Name = "Số khách")]
        public int NumberOfGuests { get; set; }

        [Display(Name = "Yêu cầu đặc biệt")]
        public string SpecialRequest { get; set; }

        // Thông tin khách hàng (nếu chưa đăng nhập)
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string CustomerPhone { get; set; }
    }
}