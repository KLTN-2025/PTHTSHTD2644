using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Helpers;
using SmartTable.Models;
using SmartTable.Models.ViewModels;

namespace SmartTable.Areas.Admin.Controllers
{
    public class AdminRevenueController : Controller
    {
        private readonly Entities db = new Entities();

        private const decimal BOOKING_FEE_RATE_DEFAULT = 0.05m; // 5%

        [HttpGet]
        public ActionResult Index(DateTime? from, DateTime? to, int? restaurantId = null)
        {
            if (!IsAdmin()) return RedirectToAction("Index", "Home", new { area = "" });

            var fromDate = (from ?? DateTime.Today.AddDays(-180)).Date;
            var toDate = (to ?? DateTime.Today).Date;
            if (fromDate > toDate) { var t = fromDate; fromDate = toDate; toDate = t; }
            var toExclusive = toDate.AddDays(1);

            ViewBag.Restaurants = db.Restaurants.AsNoTracking()
                .OrderBy(r => r.name)
                .Select(r => new SelectListItem
                {
                    Value = r.restaurant_id.ToString(),
                    Text = r.name,
                    Selected = restaurantId.HasValue && r.restaurant_id == restaurantId.Value
                }).ToList();

            // 1) payments theo paid_at (cash basis)
            var payQuery = db.Payments.AsNoTracking()
                .Where(p => p.booking_id.HasValue
                            && p.paid_at.HasValue
                            && p.paid_at.Value >= fromDate
                            && p.paid_at.Value < toExclusive);

            if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                var bid = db.Bookings.AsNoTracking()
                    .Where(b => b.restaurant_id.HasValue && b.restaurant_id.Value == restaurantId.Value)
                    .Select(b => b.booking_id)
                    .ToList();

                payQuery = payQuery.Where(p => bid.Contains(p.booking_id.Value));
            }

            var pays = payQuery
                .Where(p => p.amount != 0m)
                .ToList();

            if (pays.Count == 0)
            {
                var emptyVm = new AdminRevenueViewModel
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    RestaurantId = restaurantId,
                    Rows = new List<AdminRevenueRow>()
                };
                return View("~/Areas/Admin/Views/AdminRevenue/Index.cshtml", emptyVm);
            }

            var bookingIds = pays.Select(x => x.booking_id.Value).Distinct().ToList();

            var bookingMap = db.Bookings.AsNoTracking()
                .Include("Restaurants")
                .Where(b => bookingIds.Contains(b.booking_id))
                .ToList()
                .ToDictionary(b => b.booking_id, b => b);

            var temp = new Dictionary<string, AdminRevenueRow>();
            var feeRate = BOOKING_FEE_RATE_DEFAULT;

            foreach (var p in pays)
            {
                if (!p.booking_id.HasValue) continue;

                var bookingId = p.booking_id.Value;
                if (!bookingMap.ContainsKey(bookingId)) continue;

                var b = bookingMap[bookingId];
                var r = b.Restaurants;
                if (r == null) continue;

                if (!p.paid_at.HasValue) continue;
                var paidAt = p.paid_at.Value;

                int y = paidAt.Year;
                int m = paidAt.Month;

                var key = $"{r.restaurant_id}-{y}-{m}";

                if (!temp.TryGetValue(key, out var row))
                {
                    row = new AdminRevenueRow
                    {
                        Year = y,
                        Month = m,

                        RestaurantId = r.restaurant_id,
                        RestaurantName = r.name,
                        ServicePackage = r.ServicePackage,

                        BusinessUserId = r.user_id,
                        BusinessNameOrEmail = "",

                        BookingFeeRate = feeRate
                    };
                    temp[key] = row;
                }

                var kind = NormalizeKind(p.payment_kind, p.status, p.amount);
                var amount = p.amount;

                // ====== HOÀN TIỀN ======
                if (IsRefundKind(kind, amount, p.status))
                {
                    var abs = Math.Abs(amount);

                    if (kind == "refund_deposit")
                        row.RefundDepositAbs += abs;
                    else if (kind == "refund_preorder")
                        row.RefundPreOrderAbs += abs;
                    else
                    {
                        row.RefundDepositAbs += abs;
                    }

                    continue;
                }

                // ====== THU TIỀN (amount > 0) ======
                if (amount <= 0m) continue;

                if (kind == "deposit")
                {
                    row.DepositPaid += amount;
                }
                else if (kind == "preorder")
                {
                    row.PreOrderPaid += amount;
                }
                else
                {
                    row.DepositPaid += amount;
                }
            }

            var businessIds = temp.Values
                .Select(x => x.BusinessUserId)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .Distinct()
                .ToList();

            var userMap = db.Users.AsNoTracking()
                .Where(u => businessIds.Contains(u.user_id))
                .Select(u => new { u.user_id, u.email, u.full_name })
                .ToList()
                .ToDictionary(
                    x => x.user_id,
                    x => (x.full_name ?? x.email ?? ("Business#" + x.user_id))
                );

            var rows = temp.Values.ToList();

            foreach (var row in rows)
            {
                if (row.BusinessUserId.HasValue && userMap.ContainsKey(row.BusinessUserId.Value))
                    row.BusinessNameOrEmail = userMap[row.BusinessUserId.Value];
                else
                    row.BusinessNameOrEmail = row.BusinessUserId.HasValue ? ("Business#" + row.BusinessUserId.Value) : "Chưa có chủ nhà hàng";

                var gross = row.DepositNet + row.PreOrderNet;
                if (gross < 0m) gross = 0m;
                row.GrossRevenue = gross;

                row.BookingFeeDue = Math.Round(row.DepositNet * row.BookingFeeRate, 0);

                // ====== PHÍ GÓI ======
                row.PlanFeeDue = (row.GrossRevenue > 0m) ? BillingHelper.GetPlanFee(row.ServicePackage) : 0m;

                row.FeesDue = row.BookingFeeDue + row.PlanFeeDue;

                row.FeesCollected = Math.Min(row.GrossRevenue, row.FeesDue);
                row.DebtAdded = row.FeesDue - row.FeesCollected;

                row.NetToRestaurant = row.GrossRevenue - row.FeesCollected;
            }

            rows = rows
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ThenByDescending(x => x.GrossRevenue)
                .ToList();

            var vm = new AdminRevenueViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                RestaurantId = restaurantId,

                Rows = rows,
                TotalGrossRevenue = rows.Sum(x => x.GrossRevenue),
                TotalFeesDue = rows.Sum(x => x.FeesDue),
                TotalFeesCollected = rows.Sum(x => x.FeesCollected),
                TotalDebtAdded = rows.Sum(x => x.DebtAdded),
                TotalNetToRestaurant = rows.Sum(x => x.NetToRestaurant)
            };

            return View("~/Areas/Admin/Views/AdminRevenue/Index.cshtml", vm);
        }

        // ================== KIND NORMALIZATION ==================
        private string NormalizeKind(string paymentKind, string status, decimal amount)
        {
            var kind = (paymentKind ?? "").Trim().ToLowerInvariant();
            var st = (status ?? "").Trim();

            if (kind == "deposit" || kind.Contains("cọc")) return "deposit";
            if (kind == "preorder" || kind.Contains("món")) return "preorder";

            if (kind == "refund_deposit" || kind == "refunddeposit" || kind.Contains("hoàn cọc")) return "refund_deposit";
            if (kind == "refund_preorder" || kind == "refundpreorder" || kind.Contains("hoàn món")) return "refund_preorder";

            if (amount < 0m)
            {
                if (st.IndexOf("món", StringComparison.OrdinalIgnoreCase) >= 0) return "refund_preorder";
                return "refund_deposit";
            }

            if (st.IndexOf("món", StringComparison.OrdinalIgnoreCase) >= 0) return "preorder";

            return "deposit";
        }

        private bool IsRefundKind(string kind, decimal amount, string status)
        {
            if (kind == "refund_deposit" || kind == "refund_preorder") return true;
            if (amount < 0m) return true;

            var st = (status ?? "").Trim();
            return st.IndexOf("hoàn", StringComparison.OrdinalIgnoreCase) >= 0
                || st.IndexOf("refund", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsAdmin()
        {
            var role = (Session["role"] ?? Session["Role"] ?? "").ToString();
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
