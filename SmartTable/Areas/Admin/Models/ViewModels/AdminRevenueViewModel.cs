using System;
using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class AdminRevenueViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? RestaurantId { get; set; }

        public decimal TotalDepositPaid { get; set; }        
        public decimal TotalPreOrderPaid { get; set; }       

        public decimal TotalRefundDepositAbs { get; set; }    
        public decimal TotalRefundPreOrderAbs { get; set; }  

        public decimal TotalGrossRevenue { get; set; }        

        // Phí admin
        public decimal TotalBookingHoldFeeDue { get; set; } 
        public decimal TotalPlanFeeDue { get; set; }        
        public decimal TotalFeesDue { get; set; }         

        public decimal TotalFeesCollected { get; set; }      
        public decimal TotalDebtAdded { get; set; }         
        public decimal TotalNetToRestaurant { get; set; }    

        public List<AdminRevenueRow> Rows { get; set; } = new List<AdminRevenueRow>();
    }

    public class AdminRevenueRow
    {
        // ================== GROUP KEY ==================
        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodLabel => $"{Month:00}/{Year}";

        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string ServicePackage { get; set; } 

        public int? BusinessUserId { get; set; }
        public string BusinessNameOrEmail { get; set; }

        public decimal DepositPaid { get; set; }      
        public decimal PreOrderPaid { get; set; }     

        public decimal RefundDepositAbs { get; set; } 
        public decimal RefundPreOrderAbs { get; set; } 

        public decimal RefundTotalAbs => RefundDepositAbs + RefundPreOrderAbs;

        private decimal? _grossRevenue;
        public decimal GrossRevenue
        {
            get => _grossRevenue ?? Math.Max(0m, (DepositPaid + PreOrderPaid) - RefundTotalAbs);
            set => _grossRevenue = value;
        }

        public decimal BookingHoldFeeRate { get; set; }
        public decimal BookingHoldFeeDue { get; set; } 

        public decimal PlanFeeDue { get; set; }

        public decimal FeesDue { get; set; }

        public decimal FeesCollected { get; set; }
        public decimal DebtAdded { get; set; }

        public decimal NetToRestaurant { get; set; }


        public decimal DepositNet => Math.Max(0m, DepositPaid - RefundDepositAbs);
        public decimal PreOrderNet => Math.Max(0m, PreOrderPaid - RefundPreOrderAbs);
        public decimal RefundAbs => RefundTotalAbs;

        public decimal BookingFeeRate
        {
            get => BookingHoldFeeRate;
            set => BookingHoldFeeRate = value;
        }
        public decimal BookingFeeDue
        {
            get => BookingHoldFeeDue;
            set => BookingHoldFeeDue = value;
        }

        public decimal TotalAmount => GrossRevenue;
    }
}
