// Models/ViewModels/PaymentViewModel.cs
using System;

namespace SmartTable.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int BookingId { get; set; }

        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantImage { get; set; }

        public DateTime BookingTime { get; set; }

        public DateTime? CreatedAt { get; set; }

        public int NumberOfGuests { get; set; }
        public string SpecialRequest { get; set; }

        public decimal Amount { get; set; }

        public decimal? DepositAmount { get; set; }
        public decimal? PreOrderTotal { get; set; }

        public string PreOrderText { get; set; }   
        public string PreOrderNote { get; set; }   

        public string PaymentMethod { get; set; }
        public string Status { get; set; }
        public string TransferContent { get; set; }

        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }

        public string BankId { get; set; }
        public string AccountNo { get; set; }
        public string AccountName { get; set; }

        public decimal DepositValue => DepositAmount.GetValueOrDefault(0m);
        public decimal PreOrderValue => PreOrderTotal.GetValueOrDefault(0m);
        public decimal TotalValue => (Amount > 0m) ? Amount : (DepositValue + PreOrderValue);
    }
}
