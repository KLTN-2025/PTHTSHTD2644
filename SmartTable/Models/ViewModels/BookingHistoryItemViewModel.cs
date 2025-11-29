using System;

namespace SmartTable.Models.ViewModels
{
    public class BookingHistoryItemViewModel
    {
        public int BookingId { get; set; }

        // Nhà hàng
        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantImage { get; set; }

        // Đặt bàn
        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequest { get; set; }

        // Trạng thái booking
        public string BookingStatus { get; set; }

        // Thanh toán
        public decimal? DepositAmount { get; set; }
        public string PaymentStatus { get; set; }     // Đã thanh toán / Chưa thanh toán
    }
}
