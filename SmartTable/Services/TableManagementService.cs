using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Services
{
    public interface ITableManagementService
    {
        RestaurantTableStatusViewModel GetRestaurantTableStatus(int restaurantId);
        BookingConflictCheckViewModel CheckBookingConflict(int restaurantId, DateTime bookingTime, int numberOfGuests, int durationMinutes = 90);
        bool UpdateTableStatus(TableStatusUpdateViewModel model);
        bool CheckInTable(int tableId, int bookingId);
        bool CheckOutTable(int tableId);
        Tables AutoAssignTable(int restaurantId, int numberOfGuests, DateTime bookingTime, int durationMinutes = 90);
        List<Tables> FindAvailableTables(int restaurantId, int requiredCapacity);
    }

    public class TableManagementService : ITableManagementService
    {
        private readonly Entities _db;

        private static readonly string[] ValidBookingStatuses =
        {
            "Đã đặt",
            "Chờ xác nhận",
            "Đã cọc",
            "Đã thanh toán",
            "confirmed",
            "checked-in"
        };

        public TableManagementService(Entities db)
        {
            _db = db;
        }

     
        public RestaurantTableStatusViewModel GetRestaurantTableStatus(int restaurantId)
        {
            var restaurant = _db.Restaurants
                .Include(r => r.Tables)
                .FirstOrDefault(r => r.restaurant_id == restaurantId);

            if (restaurant == null)
                return new RestaurantTableStatusViewModel();

            var tables = restaurant.Tables.ToList();
            var now = DateTime.Now;
            const int defaultDurationMinutes = 90;

            var bookings = _db.Bookings
                .Where(b => b.restaurant_id == restaurantId && b.table_id != null)
                .Include(b => b.Users)
                .ToList()
                .Where(b => IsValidBookingStatus(b.status))
                .ToList();

            var tableViewModels = new List<TableManagementViewModel>();

            foreach (var table in tables)
            {
                var tableBookings = bookings
                    .Where(b => b.table_id == table.table_id)
                    .ToList();

                var checkedIn = tableBookings
                    .Where(b => IsCheckedIn(b.status))
                    .OrderByDescending(b => b.booking_time)
                    .FirstOrDefault();

                Bookings currentBooking = checkedIn;

                if (currentBooking == null)
                {
                    currentBooking = tableBookings
                        .Where(b => b.booking_time.AddMinutes(defaultDurationMinutes) > now)
                        .OrderBy(b => b.booking_time)
                        .FirstOrDefault();
                }

                var vm = new TableManagementViewModel
                {
                    TableId = table.table_id,
                    RestaurantId = table.restaurant_id ?? 0,
                    TableNumber = table.table_number,
                    Capacity = table.capacity,
                    Type = string.IsNullOrWhiteSpace(table.type) ? "Thường" : table.type,
                    IsAvailable = table.is_available ?? true,

                    CurrentBookingId = currentBooking != null ? (int?)currentBooking.booking_id : null,
                    CurrentCustomerName = currentBooking?.Users?.full_name,
                    CurrentBookingTime = currentBooking != null ? (DateTime?)currentBooking.booking_time : null,

                    CurrentStatus = GetTableStatus(table, currentBooking),
                    CheckInTime = (currentBooking != null && IsCheckedIn(currentBooking.status))
                        ? (DateTime?)currentBooking.booking_time
                        : null,
                    CheckOutTime = null
                };

                if (vm.CheckInTime.HasValue && vm.CurrentStatus == "occupied")
                {
                    vm.UsageDurationMinutes = (int)(DateTime.Now - vm.CheckInTime.Value).TotalMinutes;
                }

                tableViewModels.Add(vm);
            }

            return new RestaurantTableStatusViewModel
            {
                RestaurantId = restaurantId,
                RestaurantName = restaurant.name,
                Tables = tableViewModels,
                TotalTables = tables.Count,
                AvailableTables = tableViewModels.Count(t => t.CurrentStatus == "available"),
                OccupiedTables = tableViewModels.Count(t => t.CurrentStatus == "occupied"),
                ReservedTables = tableViewModels.Count(t => t.CurrentStatus == "reserved"),
                MaintenanceTables = tableViewModels.Count(t => t.CurrentStatus == "maintenance")
            };
        }

    
        public BookingConflictCheckViewModel CheckBookingConflict(int restaurantId, DateTime bookingTime, int numberOfGuests, int durationMinutes = 90)
        {
            var result = new BookingConflictCheckViewModel
            {
                RestaurantId = restaurantId,
                BookingTime = bookingTime,
                NumberOfGuests = numberOfGuests,
                DurationMinutes = durationMinutes
            };

            var bookingEnd = bookingTime.AddMinutes(durationMinutes);

            var tables = _db.Tables
                .Where(t => t.restaurant_id == restaurantId && (t.is_available == null || t.is_available == true))
                .ToList()
                .Where(t => t.capacity >= numberOfGuests)
                .ToList();

            var bookings = _db.Bookings
                .Where(b => b.restaurant_id == restaurantId && b.table_id != null)
                .ToList()
                .Where(b => IsValidBookingStatus(b.status))
                .Select(b => new
                {
                    TableId = b.table_id.Value,
                    Start = b.booking_time
                })
                .ToList();

            foreach (var table in tables)
            {
                bool hasConflict = bookings.Any(b =>
                {
                    if (b.TableId != table.table_id) return false;

                    var bStart = b.Start;
                    var bEnd = bStart.AddMinutes(durationMinutes);

                    return Overlap(bStart, bEnd, bookingTime, bookingEnd);
                });

                if (!hasConflict) result.AvailableTableIds.Add(table.table_id);
                else result.ConflictTableIds.Add(table.table_id);
            }

            result.HasConflict = result.AvailableTableIds.Count == 0;
            return result;
        }

 
        public bool UpdateTableStatus(TableStatusUpdateViewModel model)
        {
            var table = _db.Tables.FirstOrDefault(t => t.table_id == model.TableId);
            if (table == null) return false;

            if (string.Equals(model.NewStatus, "available", StringComparison.OrdinalIgnoreCase))
                table.is_available = true;
            else if (string.Equals(model.NewStatus, "maintenance", StringComparison.OrdinalIgnoreCase))
                table.is_available = false;

            _db.SaveChanges();
            return true;
        }


        public bool CheckInTable(int tableId, int bookingId)
        {
            try
            {
                var table = _db.Tables.FirstOrDefault(t => t.table_id == tableId);
                if (table == null) return false;

                var booking = _db.Bookings.FirstOrDefault(b => b.booking_id == bookingId && b.table_id == tableId);
                if (booking == null) return false;

                if (!IsValidBookingStatus(booking.status)) return false;

                booking.status = "checked-in";
                table.is_available = false;

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckInTable Error: {ex}");
                return false;
            }
        }

        public bool CheckOutTable(int tableId)
        {
            try
            {
                var table = _db.Tables.FirstOrDefault(t => t.table_id == tableId);
                if (table == null) return false;

                var activeBooking = _db.Bookings
                    .Where(b => b.table_id == tableId && b.status == "checked-in")
                    .OrderByDescending(b => b.booking_id)
                    .FirstOrDefault();

                if (activeBooking != null)
                    activeBooking.status = "completed";

                table.is_available = true;

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckOutTable Error: {ex}");
                return false;
            }
        }

        public List<Tables> FindAvailableTables(int restaurantId, int requiredCapacity)
        {
            return _db.Tables
                .Where(t =>
                    t.restaurant_id == restaurantId &&
                    t.capacity >= requiredCapacity &&
                    (t.is_available == null || t.is_available == true))
                .ToList();
        }


        public Tables AutoAssignTable(int restaurantId, int numberOfGuests, DateTime bookingTime, int durationMinutes = 90)
        {
            var bookingEnd = bookingTime.AddMinutes(durationMinutes);

            var tables = _db.Tables
                .Where(t =>
                    t.restaurant_id == restaurantId &&
                    (t.is_available == true || t.is_available == null) &&
                    t.capacity >= numberOfGuests)
                .OrderBy(t => t.capacity)
                .ToList();

            var bookingsByTable = _db.Bookings
                .Where(b => b.restaurant_id == restaurantId && b.table_id != null)
                .ToList()
                .Where(b => IsValidBookingStatus(b.status))
                .GroupBy(b => b.table_id.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var table in tables)
            {
                if (!bookingsByTable.TryGetValue(table.table_id, out var existing))
                    return table;

                bool conflict = existing.Any(b =>
                {
                    var bStart = b.booking_time;
                    var bEnd = bStart.AddMinutes(durationMinutes);
                    return Overlap(bStart, bEnd, bookingTime, bookingEnd);
                });

                if (!conflict)
                    return table;
            }

            return null;
        }


        private bool IsValidBookingStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            if (status.Equals("confirmed", StringComparison.OrdinalIgnoreCase)) return true;
            if (status.Equals("checked-in", StringComparison.OrdinalIgnoreCase)) return true;

            return status.IndexOf("Đã thanh toán", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("Đã cọc", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("Đã đặt", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("Chờ xác nhận", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCheckedIn(string status)
        {
            return !string.IsNullOrWhiteSpace(status)
                   && status.Equals("checked-in", StringComparison.OrdinalIgnoreCase);
        }

        private bool Overlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        {
            return aStart < bEnd && aEnd > bStart;
        }

        private string GetTableStatus(Tables table, Bookings currentBooking)
        {
            if (currentBooking != null)
            {
                if (IsCheckedIn(currentBooking.status)) return "occupied";
                return "reserved";
            }

            if (table.is_available == false) return "maintenance";
            return "available";
        }
    }
}
