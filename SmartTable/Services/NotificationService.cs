using System;
using SmartTable.Models;

namespace SmartTable.Services
{
    public interface INotificationService
    {
        void Push(int userId, string type, string title, string message,
                  int? restaurantId = null, int? bookingId = null, int? reviewId = null, string link = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly Entities _db;

        public NotificationService() : this(new Entities()) { }
        public NotificationService(Entities db) { _db = db; }

        public void Push(int userId, string type, string title, string message,
                         int? restaurantId = null, int? bookingId = null, int? reviewId = null, string link = null)
        {
            _db.Notifications.Add(new Notifications
            {
                user_id = userId,
                restaurant_id = restaurantId,
                booking_id = bookingId,
                review_id = reviewId,
                type = type,
                title = title,
                message = message,
                link = link,
                is_read = false,
                created_at = DateTime.Now
            });

            _db.SaveChanges();
        }
    }
}
