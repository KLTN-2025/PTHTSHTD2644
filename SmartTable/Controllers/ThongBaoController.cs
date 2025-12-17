using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Filters;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class ThongBaoController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private int? CurrentUserId()
        {
            var u = Session["user"] as Users;
            if (u != null && u.user_id > 0) return u.user_id;

            var raw = Session["user_id"] ?? Session["UserId"] ?? Session["USER_ID"];
            if (raw == null) return null;

            int id;
            return int.TryParse(raw.ToString(), out id) && id > 0 ? id : (int?)null;
        }

        private string CurrentRole()
        {
            return (Session["role"] ?? Session["Role"] ?? "").ToString().Trim();
        }

        private bool IsBusiness()
        {
            var role = CurrentRole();
            return role.Equals("business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("DoanhNghiep", StringComparison.OrdinalIgnoreCase)
                || role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private List<int> GetOwnedRestaurantIds(int ownerId)
        {
            return db.Restaurants.AsNoTracking()
                .Where(r => r.user_id == ownerId)
                .Select(r => r.restaurant_id)
                .ToList();
        }

      
        [HttpGet]
        public ActionResult Index()
        {
            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            int userId = uid.Value;

            var list = db.Notifications.AsNoTracking()
                .Include(n => n.Restaurants)
                .Where(n => n.user_id == userId)
                .OrderByDescending(n => n.created_at)
                .Take(200)
                .ToList();

            return View("Index", list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAllRead()
        {
            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            int userId = uid.Value;

            var unread = db.Notifications
                .Where(n => n.user_id == userId && n.is_read == false)
                .ToList();

            foreach (var n in unread) n.is_read = true;

            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Open(int id)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            int userId = uid.Value;

            var noti = db.Notifications.FirstOrDefault(n => n.notification_id == id && n.user_id == userId);
            if (noti == null) return RedirectToAction("Index");

            noti.is_read = true;
            db.SaveChanges();

            if (!string.IsNullOrWhiteSpace(noti.link))
                return Redirect(noti.link);

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Business()
        {
            if (!IsBusiness())
                return RedirectToAction("Index", "Home");

            var uid = CurrentUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Business"); 

            int ownerId = uid.Value;

            var bizTypes = new[]
            {
        "BIZ_BOOKING_CREATED",
        "BIZ_BOOKING_CANCELED_BY_USER",
        "BOOKING_CANCELED_BY_RESTAURANT",   
        "DEPOSIT_CONFIRMED",                
        "review_pending", "review_reported" 
    };

            var list = db.Notifications.AsNoTracking()
                .Include(n => n.Restaurants)
                .Where(n => n.user_id == ownerId
                            && (n.type == null || bizTypes.Contains(n.type)))
                .OrderByDescending(n => n.created_at)
                .Take(300)
                .ToList();

            return View("Business", list);
        }

        [HttpGet]
        public ActionResult BusinessIndex()
        {
            return RedirectToAction("Business");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BusinessMarkAllRead()
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Business");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);

            var unread = db.Notifications
                .Where(n =>
                    n.is_read == false &&
                    (n.user_id == ownerId
                     || (n.restaurant_id.HasValue && ownedRestaurantIds.Contains(n.restaurant_id.Value))))
                .ToList();

            foreach (var n in unread) n.is_read = true;

            db.SaveChanges();
            return RedirectToAction("Business");
        }

        [HttpGet]
        public ActionResult BusinessOpen(int id)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "Home");

            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Business");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);

            var noti = db.Notifications.FirstOrDefault(n =>
                n.notification_id == id &&
                (n.user_id == ownerId
                 || (n.restaurant_id.HasValue && ownedRestaurantIds.Contains(n.restaurant_id.Value))));

            if (noti == null) return RedirectToAction("Business");

            noti.is_read = true;
            db.SaveChanges();

            if (!string.IsNullOrWhiteSpace(noti.link))
                return Redirect(noti.link);

            return RedirectToAction("Business");
        }


     
        [ChildActionOnly]
        public PartialViewResult _UserBellBadge()
        {
            var uid = CurrentUserId();
            int cnt = 0;

            if (uid.HasValue)
            {
                int userId = uid.Value;
                cnt = db.Notifications.AsNoTracking()
                    .Count(n => n.user_id == userId && n.is_read == false);
            }

            ViewBag.UnreadCount = cnt;
            return PartialView("_BellBadge");
        }

        [ChildActionOnly]
        public PartialViewResult _BizBellBadge()
        {
            int cnt = 0;

            if (IsBusiness())
            {
                var uid = CurrentUserId();
                if (uid.HasValue)
                {
                    int ownerId = uid.Value;
                    var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);

                    cnt = db.Notifications.AsNoTracking()
                        .Count(n =>
                            n.is_read == false &&
                            (n.user_id == ownerId
                             || (n.restaurant_id.HasValue && ownedRestaurantIds.Contains(n.restaurant_id.Value))));
                }
            }

            ViewBag.UnreadCount = cnt;
            return PartialView("_BellBadge");
        }
    }
}
