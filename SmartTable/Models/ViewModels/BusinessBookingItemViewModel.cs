using System;

namespace SmartTable.Models.ViewModels
{
    public class BusinessBookingItemViewModel
    {
        public int BookingId { get; set; }

        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantImage { get; set; }

        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }

        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public string SpecialRequest { get; set; }

        public string BookingStatus { get; set; }

        public string CancelReason { get; set; }

        public decimal? DepositAmount { get; set; }

       
        public string PaymentStatus { get; set; }

    }
}
