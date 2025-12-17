using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartTable.Models.ViewModels
{

    public class TableManagementViewModel
    {
        public int TableId { get; set; }
        public int RestaurantId { get; set; }
        
        [Required(ErrorMessage = "Vui lòng nhập số bàn")]
        [StringLength(20)]
        public string TableNumber { get; set; }
        
        [Required(ErrorMessage = "Vui lòng nhập sức chứa")]
        [Range(1, 100)]
        public int? Capacity { get; set; }
        
        [StringLength(50)]
        public string Type { get; set; } 
        
        public bool? IsAvailable { get; set; }
        
        
        public string CurrentStatus { get; set; }
        
        
        public int? CurrentBookingId { get; set; }
        public string CurrentCustomerName { get; set; }
        public DateTime? CurrentBookingTime { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        
        public int? UsageDurationMinutes { get; set; }
    }

    public class RestaurantTableStatusViewModel
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        
        public List<TableManagementViewModel> Tables { get; set; }
        
        public int TotalTables { get; set; }
        public int AvailableTables { get; set; }
        public int OccupiedTables { get; set; }
        public int ReservedTables { get; set; }
        public int MaintenanceTables { get; set; }
        
        public decimal OccupancyRate => TotalTables > 0 ? ((decimal)OccupiedTables / TotalTables) * 100 : 0;
        
        public RestaurantTableStatusViewModel()
        {
            Tables = new List<TableManagementViewModel>();
        }
    }

    public class BookingConflictCheckViewModel
    {
        public int RestaurantId { get; set; }
        public DateTime BookingTime { get; set; }
        public int NumberOfGuests { get; set; }
        public int DurationMinutes { get; set; } = 90;
        
        public bool HasConflict { get; set; }
        public List<int> AvailableTableIds { get; set; }
        public List<int> ConflictTableIds { get; set; }
        
        public BookingConflictCheckViewModel()
        {
            AvailableTableIds = new List<int>();
            ConflictTableIds = new List<int>();
        }
    }

 
    public class TableStatusUpdateViewModel
    {
        public int TableId { get; set; }
        public string NewStatus { get; set; } 
        public int? BookingId { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public string UpdateReason { get; set; }
    }
}