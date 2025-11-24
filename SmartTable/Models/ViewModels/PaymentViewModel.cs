using System;

namespace SmartTable.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int BookingId { get; set; }

        public string RestaurantName { get; set; }
        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }

        public decimal Amount { get; set; }          // Số tiền cần thanh toán
        public string PaymentMethod { get; set; }    // Ví dụ: "Tiền mặt", "Momo", "VNPay"
        public string Status { get; set; }           // "Pending", "Đã thanh toán"
    }
}
