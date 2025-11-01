using System;
using System.ComponentModel.DataAnnotations;

namespace SmartTable.Models.ViewModels
{
    // Lớp này dùng để chứa tất cả dữ liệu từ form đăng ký đối tác
    public class PartnerRegistrationViewModel
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Tên nhà hàng không được để trống")]
        public string RestaurantName { get; set; }

        [Required(ErrorMessage = "Tỉnh/Thành phố không được để trống")]
        public string City { get; set; }

        [Required(ErrorMessage = "Địa chỉ không được để trống")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Số cơ sở không được để trống")]
        public int BranchCount { get; set; }

        // Mảng để nhận các checkbox
        public string[] ServiceTypes { get; set; }
        public string ServiceTypeOther { get; set; }

        [Required(ErrorMessage = "Loại hình dịch vụ không được để trống")]
        public string ServiceDescription { get; set; }

        [Required(ErrorMessage = "Phong cách ẩm thực không được để trống")]
        public string CuisineStyle { get; set; }

        [Required(ErrorMessage = "Món đặc sắc không được để trống")]
        public string SignatureDishes { get; set; }

        [Required(ErrorMessage = "Hóa đơn trung bình không được để trống")]
        public string AverageBill { get; set; }
        public string AverageBillOther { get; set; }

        [Required(ErrorMessage = "Tổng số chỗ ngồi không được để trống")]
        public int TotalSeats { get; set; }

        [Required(ErrorMessage = "Số tầng không được để trống")]
        public string FloorCount { get; set; } // Dùng string vì có thể nhập "3 tầng và 1 sân thượng"

        [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
        [DataType(DataType.Date)]
        public DateTime? OpeningDate { get; set; }

        [Required(ErrorMessage = "Giờ mở cửa không được để trống")]
        public string OpeningTime { get; set; }

        [Required(ErrorMessage = "Giờ đóng cửa không được để trống")]
        public string ClosingTime { get; set; }

        [Required]
        public string SlowHours { get; set; }
        [Required]
        public string BusyHours { get; set; }
        [Required]
        public string PartnershipGoal { get; set; }
        [Required]
        public string ServicePackage { get; set; }

        [Required(ErrorMessage = "Tên người liên hệ không được để trống")]
        public string ContactName { get; set; }
        [Required]
        public string ContactRole { get; set; }
        [Required]
        public string ContactPhone { get; set; }

        // Tùy chọn
        public string Website { get; set; }
        public string SpaceDescription { get; set; }
        public string SeatingType { get; set; }
        public string PrivateRoomCount { get; set; }
        public string[] Amenities { get; set; }
        public string AmenitiesOther { get; set; }
        public string NearbyLandmark { get; set; }
        public string PhotoLink { get; set; }
        public string PreviousPartnership { get; set; }
        public string PreviousPartnershipOther { get; set; }
        public string PreviousPartnershipName { get; set; }
        public string Questions { get; set; }
    }
}