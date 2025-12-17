using System;
using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class BookingHistoryItemViewModel
    {
        public int BookingId { get; set; }
        public DateTime BookingTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int NumberOfGuests { get; set; }

        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = "Nhà hàng";
        public string RestaurantAddress { get; set; } = "";
        public string RestaurantImage { get; set; } = "";

        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";

        public string BookingStatus { get; set; } = "Chờ xác nhận";
        public string CancelReason { get; set; } = "";
        public string SpecialRequest { get; set; } = "";

        public decimal DepositAmount { get; set; }

        public decimal DepositPaidAmount { get; set; }

        public string PaymentStatus { get; set; } = "Chưa cọc";

        public string PaymentMethod { get; set; } = "Nhà hàng xác nhận";

        public bool IsDepositRefunded { get; set; }

        public bool HasPreOrder { get; set; }
        public decimal PreOrderTotalAmount { get; set; }
        public List<PreOrderItemVm> PreOrderItems { get; set; } = new List<PreOrderItemVm>();

        public class PreOrderItemVm
        {
            public string Name { get; set; } = "";
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }
}
