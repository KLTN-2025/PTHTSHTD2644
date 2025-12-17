using SmartTable.Filters;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class ReviewController : Controller
    {
        private readonly Entities db = new Entities();

        private Users CurrentUser => Session["user"] as Users;

        private int? CurrentUserId()
        {
            var u = CurrentUser;
            if (u != null && u.user_id > 0) return u.user_id;

            var raw = Session["user_id"] ?? Session["UserId"] ?? Session["USER_ID"];
            if (raw == null) return null;

            int id;
            return int.TryParse(raw.ToString(), out id) && id > 0 ? id : (int?)null;
        }

        private bool IsBusiness()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString().Trim();
            return role.Equals("Business", StringComparison.OrdinalIgnoreCase)
                || role.Equals("DoanhNghiep", StringComparison.OrdinalIgnoreCase)
                || role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase);
        }

        private int GetRestaurantOwnerId(int restaurantId)
        {
            var ownerId = db.Restaurants
                .AsNoTracking()
                .Where(r => r.restaurant_id == restaurantId)
                .Select(r => (int?)r.user_id)   
                .FirstOrDefault();

            return ownerId.GetValueOrDefault(0);
        }

        private void AddNotification(
            int toUserId,
            int? restaurantId,
            int? bookingId,
            int? reviewId,
            string type,
            string title,
            string message,
            string link
        )
        {
            if (toUserId <= 0) return;

            db.Notifications.Add(new Notifications
            {
                user_id = toUserId,
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
        }

        [HttpGet]
        public ActionResult Create(int restaurantId)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đánh giá.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = db.Restaurants.AsNoTracking().FirstOrDefault(r => r.restaurant_id == restaurantId);
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhà hàng.";
                return RedirectToAction("Index", "Home");
            }

            bool hasBooking = db.Bookings.Any(b =>
                b.user_id == uid.Value
                && b.restaurant_id == restaurantId
                && b.booking_time <= DateTime.Now
                && (
                    (b.status ?? "").Contains("Đã thanh toán")
                    || (b.status ?? "").Contains("Đã cọc")
                    || (b.status ?? "").Contains("Đã hoàn thành")
                ));

            if (!hasBooking)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể đánh giá sau khi đã có booking hợp lệ.";
                return RedirectToAction("RestaurantDetails", "DetailNhaHang", new { id = restaurantId });
            }

            var existing = db.Reviews.AsNoTracking()
                .FirstOrDefault(r => r.user_id == uid.Value && r.restaurant_id == restaurantId);

            if (existing != null)
            {
                TempData["WarningMessage"] = "Bạn đã đánh giá nhà hàng này. Bạn có thể chỉnh sửa đánh giá.";
                return RedirectToAction("Edit", new { reviewId = existing.review_id });
            }

            ViewBag.RestaurantName = restaurant.name;
            return View(new ReviewCreateViewModel
            {
                RestaurantId = restaurantId,
                Rating = 5
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ReviewCreateViewModel model)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue)
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hạn.";
                return RedirectToAction("Login", "Account");
            }

            if (!ModelState.IsValid)
            {
                var res = db.Restaurants.AsNoTracking().FirstOrDefault(r => r.restaurant_id == model.RestaurantId);
                ViewBag.RestaurantName = res?.name ?? "Nhà hàng";
                return View(model);
            }

            var restaurant = db.Restaurants.FirstOrDefault(r => r.restaurant_id == model.RestaurantId);
            if (restaurant == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy nhà hàng.";
                return RedirectToAction("Index", "Home");
            }

            var existed = db.Reviews.FirstOrDefault(r => r.user_id == uid.Value && r.restaurant_id == model.RestaurantId);
            if (existed != null)
            {
                TempData["WarningMessage"] = "Bạn đã đánh giá nhà hàng này rồi.";
                return RedirectToAction("Edit", new { reviewId = existed.review_id });
            }

            var review = new Reviews
            {
                user_id = uid.Value,                     
                restaurant_id = model.RestaurantId,       
                rating = (byte)model.Rating,
                comment = model.Comment,
                created_at = DateTime.Now,
                status = "pending",
                approved_at = null,
                approved_by = null
            };

            db.Reviews.Add(review);
            db.SaveChanges();

            int ownerId = GetRestaurantOwnerId(model.RestaurantId);
            if (ownerId > 0)
            {
                AddNotification(
                    toUserId: ownerId,
                    restaurantId: model.RestaurantId,
                    bookingId: null,
                    reviewId: review.review_id,
                    type: "review_pending",
                    title: "Có đánh giá mới cần duyệt",
                    message: $"Khách hàng vừa gửi đánh giá {review.rating}★. Bấm để xem và duyệt.",
                    link: Url.Action("Business", "ThongBao") 
                );
                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá! Đánh giá sẽ hiển thị sau khi nhà hàng duyệt.";
            return RedirectToAction("RestaurantDetails", "DetailNhaHang", new { id = model.RestaurantId });
        }

        [HttpGet]
        public ActionResult Edit(int reviewId)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập.";
                return RedirectToAction("Login", "Account");
            }

            var review = db.Reviews.FirstOrDefault(r => r.review_id == reviewId);
            if (review == null || review.user_id.GetValueOrDefault(0) != uid.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đánh giá này.";
                return RedirectToAction("Index", "Home");
            }

            var rid = review.restaurant_id.GetValueOrDefault(0);
            var restaurant = db.Restaurants.AsNoTracking().FirstOrDefault(r => r.restaurant_id == rid);

            ViewBag.RestaurantName = restaurant?.name ?? "Nhà hàng";
            ViewBag.ReviewId = reviewId;

            return View(new ReviewCreateViewModel
            {
                RestaurantId = rid,
                Rating = review.rating,
                Comment = review.comment
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int reviewId, ReviewCreateViewModel model)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue)
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập đã hết hạn.";
                return RedirectToAction("Login", "Account");
            }

            var review = db.Reviews.FirstOrDefault(r => r.review_id == reviewId);
            if (review == null || review.user_id.GetValueOrDefault(0) != uid.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đánh giá này.";
                return RedirectToAction("Index", "Home");
            }

            if (!ModelState.IsValid)
            {
                var rid = review.restaurant_id.GetValueOrDefault(0);
                var restaurant = db.Restaurants.AsNoTracking().FirstOrDefault(r => r.restaurant_id == rid);
                ViewBag.RestaurantName = restaurant?.name ?? "Nhà hàng";
                ViewBag.ReviewId = reviewId;
                return View(model);
            }

            review.rating = (byte)model.Rating;
            review.comment = model.Comment;
            review.created_at = DateTime.Now;

            review.status = "pending";
            review.approved_at = null;
            review.approved_by = null;

            db.SaveChanges();

            int rid2 = review.restaurant_id.GetValueOrDefault(0);
            int ownerId = GetRestaurantOwnerId(rid2);
            if (ownerId > 0)
            {
                AddNotification(
                    toUserId: ownerId,
                    restaurantId: rid2,
                    bookingId: null,
                    reviewId: review.review_id,
                    type: "review_pending",
                    title: "Đánh giá được cập nhật – cần duyệt lại",
                    message: "Khách hàng vừa chỉnh sửa đánh giá. Bấm để xem và duyệt.",
                    link: Url.Action("Business", "ThongBao")
                );
                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Đánh giá của bạn đã được cập nhật và sẽ chờ nhà hàng duyệt lại.";
            return RedirectToAction("RestaurantDetails", "DetailNhaHang", new { id = rid2 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int reviewId)
        {
            var uid = CurrentUserId();
            if (!uid.HasValue)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });

            var review = db.Reviews.FirstOrDefault(r => r.review_id == reviewId);
            if (review == null || review.user_id.GetValueOrDefault(0) != uid.Value)
                return Json(new { success = false, message = "Bạn không có quyền xóa đánh giá này." });

            int restaurantId = review.restaurant_id.GetValueOrDefault(0);

            db.Reviews.Remove(review);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Đánh giá đã bị xóa.",
                redirectUrl = Url.Action("RestaurantDetails", "DetailNhaHang", new { id = restaurantId })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Approve(int reviewId)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "BusinessHome");

            var u = CurrentUser;
            if (u == null) return RedirectToAction("Login", "Business");

            var rv = db.Reviews.Include(r => r.Restaurants).FirstOrDefault(r => r.review_id == reviewId);
            if (rv == null) return RedirectToAction("Business", "ThongBao");

            int resId = rv.restaurant_id.GetValueOrDefault(0);
            int ownerId = GetRestaurantOwnerId(resId);
            if (ownerId <= 0 || ownerId != u.user_id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền duyệt đánh giá này.";
                return RedirectToAction("Business", "ThongBao");
            }

            rv.status = "approved";
            rv.approved_at = DateTime.Now;
            rv.approved_by = u.full_name;

            db.SaveChanges();

            int toUserId = rv.user_id.GetValueOrDefault(0);
            if (toUserId > 0)
            {
                AddNotification(
                    toUserId: toUserId,
                    restaurantId: resId,
                    bookingId: null,
                    reviewId: rv.review_id,
                    type: "review_approved",
                    title: "Đánh giá của bạn đã được duyệt",
                    message: "Nhà hàng đã duyệt đánh giá của bạn. Cảm ơn bạn!",
                    link: Url.Action("RestaurantDetails", "DetailNhaHang", new { id = resId })
                );
                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Đã duyệt đánh giá.";
            return RedirectToAction("Business", "ThongBao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reject(int reviewId)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "BusinessHome");

            var u = CurrentUser;
            if (u == null) return RedirectToAction("Login", "Business");

            var rv = db.Reviews.Include(r => r.Restaurants).FirstOrDefault(r => r.review_id == reviewId);
            if (rv == null) return RedirectToAction("Business", "ThongBao");

            int resId = rv.restaurant_id.GetValueOrDefault(0);
            int ownerId = GetRestaurantOwnerId(resId);
            if (ownerId <= 0 || ownerId != u.user_id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền từ chối đánh giá này.";
                return RedirectToAction("Business", "ThongBao");
            }

            rv.status = "rejected";
            rv.approved_at = DateTime.Now;
            rv.approved_by = u.full_name;

            db.SaveChanges();

            int toUserId = rv.user_id.GetValueOrDefault(0);
            if (toUserId > 0)
            {
                AddNotification(
                    toUserId: toUserId,
                    restaurantId: resId,
                    bookingId: null,
                    reviewId: rv.review_id,
                    type: "review_rejected",
                    title: "Đánh giá của bạn chưa được duyệt",
                    message: "Nhà hàng đã từ chối hiển thị đánh giá của bạn.",
                    link: Url.Action("ThongBao", "ThongBao") 
                );
                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Đã từ chối đánh giá.";
            return RedirectToAction("Business", "ThongBao");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reply(int reviewId, string replyText)
        {
            if (!IsBusiness()) return RedirectToAction("Index", "BusinessHome");

            var u = CurrentUser;
            if (u == null) return RedirectToAction("Login", "Business");

            if (string.IsNullOrWhiteSpace(replyText))
            {
                TempData["ErrorMessage"] = "Nội dung phản hồi không được trống.";
                return RedirectToAction("Business", "ThongBao");
            }

            var rv = db.Reviews.Include(r => r.Restaurants).FirstOrDefault(r => r.review_id == reviewId);
            if (rv == null) return RedirectToAction("Business", "ThongBao");

            int resId = rv.restaurant_id.GetValueOrDefault(0);
            int ownerId = GetRestaurantOwnerId(resId);
            if (ownerId <= 0 || ownerId != u.user_id)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền phản hồi đánh giá này.";
                return RedirectToAction("Business", "ThongBao");
            }


            int toUserId = rv.user_id.GetValueOrDefault(0);
            if (toUserId > 0)
            {
                AddNotification(
                    toUserId: toUserId,
                    restaurantId: resId,
                    bookingId: null,
                    reviewId: rv.review_id,
                    type: "review_reply",
                    title: "Nhà hàng đã phản hồi đánh giá của bạn",
                    message: replyText.Trim(),
                    link: Url.Action("RestaurantDetails", "DetailNhaHang", new { id = resId })
                );
                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Đã gửi phản hồi cho khách.";
            return RedirectToAction("Business", "ThongBao");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
