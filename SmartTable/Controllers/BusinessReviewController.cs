using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using SmartTable.Filters;

namespace SmartTable.Controllers
{
    [AuthorizeUser]
    public class BusinessReviewController : Controller
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

        private string CurrentUserName()
        {
            var u = Session["user"] as Users;
            return (u != null && !string.IsNullOrWhiteSpace(u.full_name)) ? u.full_name : "Doanh nghiệp";
        }

        private List<int> GetOwnedRestaurantIds(int ownerId)
        {
            return db.Restaurants.AsNoTracking()
                .Where(r => r.user_id == ownerId)
                .Select(r => r.restaurant_id)
                .ToList();
        }

        private JsonResult JsonFail(int httpCode, string message)
        {
            Response.StatusCode = httpCode;
            return Json(new { success = false, message = message }, JsonRequestBehavior.AllowGet);
        }

        private JsonResult JsonOk(string message)
        {
            return Json(new { success = true, message = message }, JsonRequestBehavior.AllowGet);
        }

        private JsonResult SaveOrJsonError(string okMessage)
        {
            try
            {
                db.SaveChanges();
                return JsonOk(okMessage);
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var msg = string.Join(" | ",
                    ex.EntityValidationErrors
                      .SelectMany(e => e.ValidationErrors)
                      .Select(e => e.PropertyName + ": " + e.ErrorMessage));

                return JsonFail(400, "Validation: " + msg);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return JsonFail(400, "DbUpdate: " + msg);
            }
            catch (Exception ex)
            {
                return JsonFail(500, "Error: " + ex.Message);
            }
        }

        private void AddNotification(
            int userId,
            int? restaurantId,
            int? reviewId,
            string title,
            string message,
            string link,
            string type = "system"
        )
        {
            var noti = new Notifications
            {
                user_id = userId,
                restaurant_id = restaurantId,
                title = string.IsNullOrWhiteSpace(title) ? "Thông báo" : title.Trim(),
                message = message ?? "",
                link = link,
                is_read = false,
                created_at = DateTime.Now
            };

            try
            {
                var pType = noti.GetType().GetProperty("type");
                if (pType != null)
                    pType.SetValue(noti, string.IsNullOrWhiteSpace(type) ? "system" : type.Trim(), null);
            }
            catch { }

            try
            {
                var pReview = noti.GetType().GetProperty("review_id");
                if (pReview != null && reviewId.HasValue)
                    pReview.SetValue(noti, reviewId.Value, null);
            }
            catch { }

            db.Notifications.Add(noti);
        }

        public class ReviewActionRequest
        {
            public int reviewId { get; set; }
            public string reason { get; set; }
            public string replyText { get; set; }
        }

        [HttpGet]
        public ActionResult Index(
            string statusFilter = "all",
            string ratingFilter = "",
            string reportFilter = "all",
            string searchKeyword = "",
            string sortBy = "newest",
            int page = 1)
        {
            if (!IsBusiness())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập khu vực doanh nghiệp.";
                return RedirectToAction("Index", "Home");
            }

            var uid = CurrentUserId();
            if (!uid.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập tài khoản doanh nghiệp.";
                return RedirectToAction("Login", "Business");
            }

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds == null || ownedRestaurantIds.Count == 0)
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn chưa liên kết nhà hàng.";
                return RedirectToAction("Index", "BusinessHome");
            }

            if (ownedRestaurantIds.Count == 1)
            {
                int onlyId = ownedRestaurantIds.First();
                ViewBag.RestaurantName = db.Restaurants.AsNoTracking()
                    .Where(x => x.restaurant_id == onlyId)
                    .Select(x => x.name)
                    .FirstOrDefault() ?? "Nhà hàng";
            }
            else ViewBag.RestaurantName = "(" + ownedRestaurantIds.Count + " nhà hàng)";

            var q = db.Reviews.AsNoTracking()
                .Include(r => r.Users)
                .Where(r => r.restaurant_id.HasValue && ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
                q = q.Where(r => (r.status ?? "pending") == statusFilter);

            if (!string.IsNullOrWhiteSpace(ratingFilter))
            {
                byte rv;
                if (byte.TryParse(ratingFilter, out rv) && rv >= 1 && rv <= 5)
                    q = q.Where(r => r.rating == rv);
            }

            if (reportFilter == "reported")
                q = q.Where(r => db.ReviewReporting.Any(rp => rp.review_id == r.review_id && rp.status == "pending"));
            else if (reportFilter == "not_reported")
                q = q.Where(r => !db.ReviewReporting.Any(rp => rp.review_id == r.review_id));

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var key = searchKeyword.Trim().ToLower();
                q = q.Where(r =>
                    ((r.Users.full_name ?? "").ToLower().Contains(key)) ||
                    ((r.Users.email ?? "").ToLower().Contains(key)) ||
                    ((r.comment ?? "").ToLower().Contains(key))
                );
            }

            switch (sortBy)
            {
                case "oldest": q = q.OrderBy(r => r.created_at); break;
                case "rating_high": q = q.OrderByDescending(r => r.rating).ThenByDescending(r => r.created_at); break;
                case "rating_low": q = q.OrderBy(r => r.rating).ThenByDescending(r => r.created_at); break;
                default: q = q.OrderByDescending(r => r.created_at); break;
            }

            if (page < 1) page = 1;
            const int pageSize = 10;

            int totalCount = q.Count();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedReviews = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var pagedIds = pagedReviews.Select(r => r.review_id).ToList();

            var replies = db.ReviewReplies.AsNoTracking()
                .Where(rr => pagedIds.Contains(rr.review_id))
                .ToList();

            var pendingReports = db.ReviewReporting.AsNoTracking()
                .Where(rp => pagedIds.Contains(rp.review_id) && rp.status == "pending")
                .ToList();

            var ridList = pagedReviews.Select(x => x.restaurant_id.GetValueOrDefault(0))
                                      .Distinct().Where(x => x > 0).ToList();

            var resNameMap = db.Restaurants.AsNoTracking()
                .Where(r => ridList.Contains(r.restaurant_id))
                .Select(r => new { r.restaurant_id, r.name })
                .ToList()
                .GroupBy(x => x.restaurant_id)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault().name);

            var models = new List<ReviewManageViewModel>();
            foreach (var r in pagedReviews)
            {
                int rid = r.restaurant_id.GetValueOrDefault(0);
                string rName = (rid > 0 && resNameMap.ContainsKey(rid) && !string.IsNullOrWhiteSpace(resNameMap[rid]))
                    ? resNameMap[rid]
                    : "Nhà hàng";

                var reply = replies.FirstOrDefault(x => x.review_id == r.review_id);

                models.Add(new ReviewManageViewModel
                {
                    ReviewId = r.review_id,
                    RestaurantId = rid,
                    RestaurantName = rName,

                    UserName = (r.Users != null && !string.IsNullOrWhiteSpace(r.Users.full_name)) ? r.Users.full_name : "Khách ẩn danh",
                    UserEmail = r.Users != null ? (r.Users.email ?? "") : "",
                    UserPhone = r.Users != null ? (r.Users.phone ?? "") : "",

                    Rating = r.rating,
                    Comment = r.comment,
                    CreatedAt = r.created_at ?? DateTime.Now,

                    Status = string.IsNullOrWhiteSpace(r.status) ? "pending" : r.status,
                    ApprovedAt = r.approved_at,
                    ApprovedBy = r.approved_by,

                    ReportCount = pendingReports.Count(x => x.review_id == r.review_id),
                    IsReported = pendingReports.Any(x => x.review_id == r.review_id),

                    ReplyText = reply != null ? reply.reply_text : null,
                    ReplyCreatedAt = reply != null ? (DateTime?)reply.created_at : null
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalReviewCount = totalCount;

            ViewBag.StatusFilter = statusFilter;
            ViewBag.RatingFilter = ratingFilter;
            ViewBag.ReportFilter = reportFilter;
            ViewBag.SearchKeyword = searchKeyword;
            ViewBag.SortBy = sortBy;

            return View(models);
        }

        [HttpGet]
        public ActionResult Statistics()
        {
            if (!IsBusiness())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập.";
                return RedirectToAction("Index", "Home");
            }

            var uid = CurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Business");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds == null || ownedRestaurantIds.Count == 0)
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn chưa liên kết nhà hàng.";
                return RedirectToAction("Index", "BusinessHome");
            }

            var approvedReviews = db.Reviews.AsNoTracking()
                .Where(r => r.restaurant_id.HasValue
                         && ownedRestaurantIds.Contains(r.restaurant_id.Value)
                         && ((r.status ?? "approved") == "approved"))
                .ToList();

            int pendingReportCount =
                (from rp in db.ReviewReporting.AsNoTracking()
                 join rv in db.Reviews.AsNoTracking() on rp.review_id equals rv.review_id
                 where rp.status == "pending"
                    && rv.restaurant_id.HasValue
                    && ownedRestaurantIds.Contains(rv.restaurant_id.Value)
                 select rp.report_id).Count();

            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            int recentReportsCount =
                (from rp in db.ReviewReporting.AsNoTracking()
                 join rv in db.Reviews.AsNoTracking() on rp.review_id equals rv.review_id
                 where rv.restaurant_id.HasValue
                    && ownedRestaurantIds.Contains(rv.restaurant_id.Value)
                    && rp.created_at >= sevenDaysAgo
                 select rp.report_id).Count();

            int repliedReviewsCount = db.ReviewReplies.AsNoTracking()
                .Count(rr => ownedRestaurantIds.Contains(rr.restaurant_id));

            var model = new ReviewMetricsViewModel
            {
                TotalReviews = approvedReviews.Count,
                AverageRating = approvedReviews.Count > 0 ? approvedReviews.Average(r => (double)r.rating) : 0,

                Count1Star = approvedReviews.Count(r => r.rating == 1),
                Count2Star = approvedReviews.Count(r => r.rating == 2),
                Count3Star = approvedReviews.Count(r => r.rating == 3),
                Count4Star = approvedReviews.Count(r => r.rating == 4),
                Count5Star = approvedReviews.Count(r => r.rating == 5),

                PendingReportCount = pendingReportCount,
                RecentReportsCount = recentReportsCount,
                RepliedReviewsCount = repliedReviewsCount
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryTokenJson]
        public ActionResult ApproveReview(ReviewActionRequest req)
        {
            int reviewId = req != null ? req.reviewId : 0;
            if (reviewId <= 0) return JsonFail(400, "Thiếu reviewId.");

            if (!IsBusiness()) return JsonFail(403, "Bạn không có quyền.");

            var uid = CurrentUserId();
            if (!uid.HasValue) return JsonFail(401, "Phiên đăng nhập đã hết hạn.");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds.Count == 0) return JsonFail(400, "Chưa liên kết nhà hàng.");

            var review = db.Reviews
                .Include(r => r.Restaurants)
                .FirstOrDefault(r => r.review_id == reviewId
                    && r.restaurant_id.HasValue
                    && ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (review == null) return JsonFail(404, "Không tìm thấy đánh giá.");

            review.status = "approved";
            review.approved_at = DateTime.Now;
            review.approved_by = CurrentUserName();

            int customerId = review.user_id.GetValueOrDefault(0);
            int resId = review.restaurant_id.GetValueOrDefault(0);

            if (customerId > 0 && resId > 0)
            {
                AddNotification(
                    userId: customerId,
                    restaurantId: resId,
                    reviewId: review.review_id,
                    title: "Đánh giá của bạn đã được duyệt",
                    message: "Nhà hàng đã duyệt đánh giá của bạn. Cảm ơn bạn đã chia sẻ trải nghiệm!",
                    link: Url.Action("RestaurantDetails", "DetailNhaHang", new { id = resId }),
                    type: "review_approved"
                );
            }

            return SaveOrJsonError("Đã duyệt đánh giá.");
        }

        [HttpPost]
        [ValidateAntiForgeryTokenJson]
        public ActionResult RejectReview(ReviewActionRequest req)
        {
            int reviewId = req != null ? req.reviewId : 0;
            string reason = req != null ? req.reason : "";

            if (reviewId <= 0) return JsonFail(400, "Thiếu reviewId.");
            if (!IsBusiness()) return JsonFail(403, "Bạn không có quyền.");

            var uid = CurrentUserId();
            if (!uid.HasValue) return JsonFail(401, "Phiên đăng nhập đã hết hạn.");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds.Count == 0) return JsonFail(400, "Chưa liên kết nhà hàng.");

            var review = db.Reviews
                .Include(r => r.Restaurants)
                .FirstOrDefault(r => r.review_id == reviewId
                    && r.restaurant_id.HasValue
                    && ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (review == null) return JsonFail(404, "Không tìm thấy đánh giá.");

            review.status = "rejected";
            review.approved_at = DateTime.Now;
            review.approved_by = CurrentUserName();

            int customerId = review.user_id.GetValueOrDefault(0);
            int resId = review.restaurant_id.GetValueOrDefault(0);

            if (customerId > 0 && resId > 0)
            {
                var msg = "Đánh giá của bạn chưa được duyệt.";
                if (!string.IsNullOrWhiteSpace(reason)) msg += " Lý do: " + reason.Trim();

                AddNotification(
                    userId: customerId,
                    restaurantId: resId,
                    reviewId: review.review_id,
                    title: "Đánh giá của bạn chưa được duyệt",
                    message: msg,
                    link: Url.Action("RestaurantDetails", "DetailNhaHang", new { id = resId }),
                    type: "review_rejected"
                );
            }

            return SaveOrJsonError("Đã từ chối đánh giá.");
        }

        [HttpPost]
        [ValidateAntiForgeryTokenJson]
        public ActionResult ReplyToReview(ReviewActionRequest req)
        {
            int reviewId = req != null ? req.reviewId : 0;
            string replyText = req != null ? req.replyText : null;

            if (reviewId <= 0) return JsonFail(400, "Thiếu reviewId.");
            if (!IsBusiness()) return JsonFail(403, "Bạn không có quyền.");

            var uid = CurrentUserId();
            if (!uid.HasValue) return JsonFail(401, "Phiên đăng nhập đã hết hạn.");

            if (string.IsNullOrWhiteSpace(replyText)) return JsonFail(400, "Vui lòng nhập phản hồi.");
            replyText = replyText.Trim();
            if (replyText.Length > 500) return JsonFail(400, "Phản hồi tối đa 500 ký tự.");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds.Count == 0) return JsonFail(400, "Chưa liên kết nhà hàng.");

            var review = db.Reviews
                .Include(r => r.Restaurants)
                .FirstOrDefault(r => r.review_id == reviewId
                    && r.restaurant_id.HasValue
                    && ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (review == null) return JsonFail(404, "Không tìm thấy đánh giá.");

            var existing = db.ReviewReplies.FirstOrDefault(x => x.review_id == reviewId);
            if (existing != null)
            {
                existing.reply_text = replyText;
                try { existing.updated_at = DateTime.Now; } catch { }
            }
            else
            {
                db.ReviewReplies.Add(new ReviewReplies
                {
                    review_id = reviewId,
                    restaurant_id = review.restaurant_id.GetValueOrDefault(0),
                    reply_text = replyText,
                    created_at = DateTime.Now
                });
            }

            if (string.IsNullOrWhiteSpace(review.status) || (review.status ?? "pending") == "pending")
            {
                review.status = "approved";
                review.approved_at = DateTime.Now;
                review.approved_by = CurrentUserName();
            }

            int customerId = review.user_id.GetValueOrDefault(0);
            int resId = review.restaurant_id.GetValueOrDefault(0);

            if (customerId > 0 && resId > 0)
            {
                AddNotification(
                    userId: customerId,
                    restaurantId: resId,
                    reviewId: review.review_id,
                    title: "Nhà hàng đã phản hồi đánh giá của bạn",
                    message: "Nhà hàng vừa phản hồi: \"" + replyText + "\"",
                    link: Url.Action("RestaurantDetails", "DetailNhaHang", new { id = resId }),
                    type: "review_replied"
                );
            }

            return SaveOrJsonError("Đã lưu phản hồi.");
        }

        [HttpPost]
        [ValidateAntiForgeryTokenJson]
        public ActionResult DeleteReview(ReviewActionRequest req)
        {
            int reviewId = req != null ? req.reviewId : 0;
            if (reviewId <= 0) return JsonFail(400, "Thiếu reviewId.");

            if (!IsBusiness()) return JsonFail(403, "Bạn không có quyền.");

            var uid = CurrentUserId();
            if (!uid.HasValue) return JsonFail(401, "Phiên đăng nhập đã hết hạn.");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds.Count == 0) return JsonFail(400, "Chưa liên kết nhà hàng.");

            var review = db.Reviews.FirstOrDefault(r =>
                r.review_id == reviewId &&
                r.restaurant_id.HasValue &&
                ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (review == null) return JsonFail(404, "Không tìm thấy đánh giá hoặc không thuộc nhà hàng của bạn.");

            var replies = db.ReviewReplies.Where(x => x.review_id == reviewId).ToList();
            if (replies.Any()) db.ReviewReplies.RemoveRange(replies);

            var reports = db.ReviewReporting.Where(x => x.review_id == reviewId).ToList();
            if (reports.Any()) db.ReviewReporting.RemoveRange(reports);

            try
            {
                db.Database.ExecuteSqlCommand("DELETE FROM Notifications WHERE review_id = @p0", reviewId);
            }
            catch { /* */ }

            db.Reviews.Remove(review);
            return SaveOrJsonError("Đã xóa đánh giá.");
        }

        [HttpPost]
        [ValidateAntiForgeryTokenJson]
        public ActionResult DeleteReply(ReviewActionRequest req)
        {
            int reviewId = req != null ? req.reviewId : 0;
            if (reviewId <= 0) return JsonFail(400, "Thiếu reviewId.");

            if (!IsBusiness()) return JsonFail(403, "Bạn không có quyền.");

            var uid = CurrentUserId();
            if (!uid.HasValue) return JsonFail(401, "Phiên đăng nhập đã hết hạn.");

            int ownerId = uid.Value;
            var ownedRestaurantIds = GetOwnedRestaurantIds(ownerId);
            if (ownedRestaurantIds.Count == 0) return JsonFail(400, "Chưa liên kết nhà hàng.");

            var review = db.Reviews.AsNoTracking().FirstOrDefault(r =>
                r.review_id == reviewId &&
                r.restaurant_id.HasValue &&
                ownedRestaurantIds.Contains(r.restaurant_id.Value));

            if (review == null) return JsonFail(404, "Không tìm thấy đánh giá hoặc không thuộc nhà hàng của bạn.");

            var reply = db.ReviewReplies.FirstOrDefault(x => x.review_id == reviewId);
            if (reply == null) return JsonFail(404, "Không có phản hồi để xóa.");

            db.ReviewReplies.Remove(reply);
            return SaveOrJsonError("Đã xóa phản hồi.");
        }
    }
}
