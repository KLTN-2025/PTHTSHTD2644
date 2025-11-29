// Models/ViewModels/PaymentViewModel.cs
using System;

namespace SmartTable.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int BookingId { get; set; }

        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantImage { get; set; }

        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequest { get; set; }

        public decimal Amount { get; set; }          // Số tiền cọc
        public string PaymentMethod { get; set; }    // "Chuyển khoản", "Tiền mặt"...
        public string Status { get; set; }           // Pending / Đã thanh toán
        public string TransferContent { get; set; }  // Nội dung chuyển khoản
        public string CustomerName { get; set; }     // Tên khách
    }
}
