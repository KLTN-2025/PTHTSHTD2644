using System;
using System.Collections.Generic;

namespace SmartTable.Models.ViewModels
{
    public class AdminStatisticsViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? RestaurantId { get; set; }

        public int TotalBusinesses { get; set; }
        public int TotalRestaurants { get; set; }

        public int TotalBookingsInRange { get; set; }
        public int PaidBookingsInRange { get; set; }
        public int CanceledBookingsInRange { get; set; }

    

        public decimal DepositPaid { get; set; }          
        public decimal PreOrderPaid { get; set; }         

        public decimal RefundDepositAbs { get; set; }    
        public decimal RefundPreOrderAbs { get; set; }   

        public decimal TotalDeposit { get; set; }        
        public decimal TotalPreOrder { get; set; }        

        public decimal TotalRevenue => TotalDeposit + TotalPreOrder;

        public decimal RestaurantGrossRevenue => TotalRevenue;

        // =========================================================
        // 2) DOANH THU ADMIN (CÓ TÍNH TIỀN GÓI + PHÍ GIỮ CHỖ)
        //    - Admin thu = phí giữ chỗ + phí gói
        //    - Có thể hiển thị thêm đã thu / công nợ
        // =========================================================
        public decimal BookingFeeRate { get; set; }       

        public decimal TotalBookingFeeDue { get; set; }   
        public decimal TotalPlanFeeDue { get; set; }     

        public decimal TotalFeesDue => TotalBookingFeeDue + TotalPlanFeeDue;

        public decimal TotalFeesCollected { get; set; }   
        public decimal TotalDebtAdded { get; set; }      

        public decimal AdminRevenueCollected => TotalFeesCollected;

        public decimal TotalNetToRestaurant { get; set; }

        public List<RestaurantStatItem> RestaurantStats { get; set; } = new List<RestaurantStatItem>();
        public List<DailyStatItem> DailyStats { get; set; } = new List<DailyStatItem>();
        public List<HourlyStatItem> HourlyStats { get; set; } = new List<HourlyStatItem>();

        public List<MenuItemStatItem> TopMenuItems { get; set; } = new List<MenuItemStatItem>();
        public List<CustomerStatItem> TopCustomers { get; set; } = new List<CustomerStatItem>();

        public PeakHourSummary PeakHour { get; set; } = new PeakHourSummary();
    }
}
