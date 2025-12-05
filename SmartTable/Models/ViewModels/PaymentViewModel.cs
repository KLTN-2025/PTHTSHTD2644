// Models/ViewModels/PaymentViewModel.cs
using System;

namespace SmartTable.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int BookingId { get; set; }

        public int RestaurantId { get; set; }          // nếu đã có
        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantImage { get; set; }

        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequest { get; set; }

        public decimal Amount { get; set; }
        public decimal? DepositAmount { get; set; }
        public decimal? PreOrderTotal { get; set; }

        // ✅ THÊM 2 DÒNG NÀY (nếu chưa có)
        public string PreOrderText { get; set; }   // text đã parse sẵn "2 x Gà; 1 x Lẩu..."
        public string PreOrderNote { get; set; }   // nếu View dùng tên này

        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public string TransferContent { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }

        public string BankId { get; set; }
        public string AccountNo { get; set; }
        public string AccountName { get; set; }
    }
}
