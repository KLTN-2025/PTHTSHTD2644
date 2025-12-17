using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using System;
using System.Collections.Generic;

namespace SmartTable.Controllers
{
    public class DetailNhaHangController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult RestaurantDetails(int id)
        {
            var restaurant = db.Restaurants
                               .Include(r => r.RestaurantImages)
                               .FirstOrDefault(r => r.restaurant_id == id && r.is_approved == true);

            if (restaurant == null)
                return HttpNotFound();

            var reviews = db.Reviews
                            .Where(r => r.restaurant_id == id && (r.status == "approved" || r.status == null))
                            .Include(r => r.Users)
                            .OrderByDescending(x => x.created_at)
                            .ToList();

            var reviewIds = reviews.Select(r => r.review_id).ToList();
            
            var replies = db.ReviewReplies
                            .Where(rr => reviewIds.Contains(rr.review_id))
                            .ToList();

            var reports = db.ReviewReporting
                            .Where(rp => reviewIds.Contains(rp.review_id) && rp.status == "pending")
                            .ToList();

            var reviewDisplayModels = reviews.Select(r => new ReviewDisplayViewModel
            {
                ReviewId = r.review_id,
                UserName = r.Users?.full_name ?? "Khách ẩn danh",
                UserInitial = !string.IsNullOrEmpty(r.Users?.full_name) ? r.Users.full_name.Substring(0, 1) : "K",
                Rating = r.rating,
                Comment = r.comment,
                CreatedAt = r.created_at ?? DateTime.Now,
                
                ReplyText = replies
                    .Where(rr => rr.review_id == r.review_id)
                    .Select(rr => rr.reply_text)
                    .FirstOrDefault(),
                ReplyCreatedAt = replies
                    .Where(rr => rr.review_id == r.review_id)
                    .Select(rr => (DateTime?)rr.created_at)
                    .FirstOrDefault(),
                
                ReportCount = reports.Count(rp => rp.review_id == r.review_id),
                IsReported = reports.Any(rp => rp.review_id == r.review_id)
            }).ToList();

            var vm = new SmartTableDetailViewModel
            {
                Restaurant = restaurant,
                MenuItems = db.MenuItems
                              .Where(m => m.restaurant_id == id && (m.is_available ?? true))
                              .ToList(),
                Reviews = reviews,
                ReviewDisplayModels = reviewDisplayModels
            };

            return View(vm);
        }

        // POST: /DetailNhaHang/PostReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PostReview(int restaurant_id, byte rating, string comment)
        {
            var user = Session["user"] as Users;
            if (user == null)
            {
                TempData["ErrorMessage"] = "Bạn cần đăng nhập để đánh giá.";
                return RedirectToAction("Login", "Account");
            }

            var restaurant = db.Restaurants.Find(restaurant_id);
            if (restaurant == null || restaurant.is_approved != true)
            {
                TempData["ErrorMessage"] = "Nhà hàng không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            if (rating < 1) rating = 1;
            if (rating > 5) rating = 5;

            if (string.IsNullOrWhiteSpace(comment))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập nội dung đánh giá.";
                return RedirectToAction("RestaurantDetails", new { id = restaurant_id });
            }

            var review = new Reviews
            {
                user_id = user.user_id,
                restaurant_id = restaurant_id,
                rating = rating,
                comment = comment.Trim(),
                created_at = DateTime.Now,
                status = "pending"
            };

            db.Reviews.Add(review);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá! Đánh giá sẽ được duyệt trong thời gian sớm nhất.";
            return RedirectToAction("RestaurantDetails", new { id = restaurant_id });
        }

        /// POST: Báo cáo đánh giá không phù hợp
        [HttpPost]
        public ActionResult ReportReview(int reviewId, string reportType, string reportReason)
        {
            var user = Session["user"] as Users;
            if (user == null)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để báo cáo." });
            }

            var review = db.Reviews.FirstOrDefault(r => r.review_id == reviewId);
            if (review == null)
            {
                return Json(new { success = false, message = "Đánh giá không tìm thấy." });
            }

            if (string.IsNullOrWhiteSpace(reportType))
            {
                return Json(new { success = false, message = "Vui lòng chọn loại báo cáo." });
            }

            if (string.IsNullOrWhiteSpace(reportReason) || reportReason.Length < 10)
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do chi tiết (tối thiểu 10 ký tự)." });
            }

            if (reportReason.Length > 500)
            {
                return Json(new { success = false, message = "Lý do báo cáo tối đa 500 ký tự." });
            }

            try
            {
                var existingReport = db.ReviewReporting.FirstOrDefault(r =>
                    r.review_id == reviewId &&
                    r.reported_by_user_id == user.user_id);

                if (existingReport != null)
                {
                    return Json(new { success = false, message = "Bạn đã báo cáo đánh giá này rồi." });
                }

                var report = new ReviewReporting
                {
                    review_id = reviewId,
                    reported_by_user_id = user.user_id,
                    report_type = reportType,
                    report_reason = reportReason.Trim(),
                    status = "pending",
                    created_at = DateTime.Now
                };

                db.ReviewReporting.Add(report);
                db.SaveChanges();

                return Json(new { success = true, message = "Báo cáo đã được gửi. Cảm ơn bạn đã giúp cải thiện cộng đồng của chúng tôi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}
