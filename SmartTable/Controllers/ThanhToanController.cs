using SmartTable.Models;
using SmartTable.Models.ViewModels;
using SmartTable.Models.Vnpay;
using SmartTable.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    public class ThanhToanController : Controller
    {
        private readonly Entities db = new Entities();
        private readonly IVnPayService _vnPayService;

        private const decimal DEPOSIT_PER_GUEST = 50000m;

        public ThanhToanController() : this(new VnPayService()) { }

        public ThanhToanController(IVnPayService vnPayService)
        {
            _vnPayService = vnPayService;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ===================== 1) CHECKOUT (HIỂN THỊ UI) =====================
        [HttpGet]
        public ActionResult Checkout(int bookingId)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                .Include(b => b.Restaurants)
                .Include(b => b.Users)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            int guests = (booking.number_of_guests <= 0) ? 1 : booking.number_of_guests;
            decimal deposit = guests * DEPOSIT_PER_GUEST;

            var pre = BuildPreOrderInfo(bookingId);

            decimal totalToPay = deposit + pre.Total;

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,

                RestaurantId = booking.restaurant_id ?? 0,
                RestaurantName = booking.Restaurants?.name ?? "Đang cập nhật",
                RestaurantAddress = booking.Restaurants?.address ?? "",
                RestaurantImage = string.IsNullOrWhiteSpace(booking.Restaurants?.Image)
                    ? "https://via.placeholder.com/100"
                    : booking.Restaurants.Image,

                BookingTime = booking.booking_time,
                NumberOfGuests = guests,
                SpecialRequest = booking.special_request ?? "",

                DepositAmount = deposit,       
                PreOrderTotal = pre.Total,        
                PreOrderNote = pre.Note,          
                PreOrderText = pre.Text,         

                Amount = totalToPay,

                BankId = "MB",
                AccountNo = "0941326591",
                AccountName = "SMART TABLE",
                TransferContent = $"SMARTTABLE-{booking.booking_id}",
                Status = "Pending",

                CustomerName = booking.Users?.full_name ?? "Khách",
                CustomerPhone = booking.Users?.phone ?? ""
            };

            return View(vm);
        }

        // ===================== 2) CHUYỂN KHOẢN: KHÁCH BẤM "TÔI ĐÃ CK" =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmBankTransfer(int bookingId, string returnUrl)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                .Include(b => b.Payments)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            bool hasPending = booking.Payments != null && booking.Payments.Any(p =>
                p != null
                && p.booking_id == bookingId
                && (p.status ?? "").ToUpperInvariant().Contains("PENDING")
                && (p.payment_kind ?? "").ToLowerInvariant() == "deposit"
            );

            if (hasPending)
            {
                TempData["SuccessMessage"] = "Bạn đã gửi yêu cầu xác nhận trước đó. Vui lòng chờ nhà hàng xác nhận.";
                return SafeRedirect(returnUrl, "DanhSachBanDaDat", "DatBan");
            }

            int guests = (booking.number_of_guests <= 0) ? 1 : booking.number_of_guests;
            decimal deposit = guests * DEPOSIT_PER_GUEST;
            var pre = BuildPreOrderInfo(bookingId);
            decimal totalToConfirm = deposit + pre.Total;

            db.Payments.Add(new Payments
            {
                booking_id = bookingId,
                payment_method = "BANK_TRANSFER",
                payment_kind = "deposit",
                amount = totalToConfirm,
                status = "PENDING_CONFIRM",
                paid_at = null,
                transaction_id = "BANK-" + Guid.NewGuid().ToString("N")
            });

            booking.status = "Chờ xác nhận thanh toán";
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã gửi yêu cầu xác nhận chuyển khoản. Nhà hàng sẽ xác nhận sớm.";
            return SafeRedirect(returnUrl, "DanhSachBanDaDat", "DatBan");
        }

        // ===================== 3) VNPAY: TẠO URL =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PayWithVnPay(int bookingId, double amount = 0, string description = null)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                .Include(b => b.Users)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            int guests = (booking.number_of_guests <= 0) ? 1 : booking.number_of_guests;
            decimal deposit = guests * DEPOSIT_PER_GUEST;
            var pre = BuildPreOrderInfo(bookingId);
            decimal total = deposit + pre.Total;

            double defaultAmount = (double)total;
            double payAmount = (amount > 0) ? amount : defaultAmount;

            var model = new PaymentInformationModel
            {
                booking_id = bookingId,
                Amount = payAmount,
                OrderDescription = string.IsNullOrWhiteSpace(description)
                    ? $"Thanh toán đặt cọc + món đặt trước #{bookingId}"
                    : description,
                OrderType = "billpayment",
                Name = booking.Users?.full_name ?? "Khách"
            };

            var url = _vnPayService.CreatePaymentUrl(model, Request);
            return Redirect(url);
        }

        // ===================== 4) VNPAY CALLBACK (RETURN URL) =====================
        [HttpGet]
        public ActionResult PaymentCallbackVnpay()
        {
            var vnpay = new VnPayLibrary();

            foreach (string key in Request.QueryString.Keys)
            {
                var value = Request.QueryString[key];
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpay.AddResponseData(key, value);
            }

            string hashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
            string secureHash = Request.QueryString["vnp_SecureHash"];

            bool ok = vnpay.ValidateSignature(secureHash, hashSecret);

            string responseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string txnRef = vnpay.GetResponseData("vnp_TxnRef");
            string amountStr = vnpay.GetResponseData("vnp_Amount");
            string transNo = vnpay.GetResponseData("vnp_TransactionNo");

            int bookingId;
            int.TryParse(txnRef, out bookingId);

            decimal amount = 0m;
            if (!string.IsNullOrEmpty(amountStr) && long.TryParse(amountStr, out var raw))
                amount = raw / 100m;

            if (!ok)
            {
                ViewBag.Message = "Sai chữ ký (checksum).";
                return View("PaymentError");
            }

            if (responseCode == "00")
            {
                var booking = db.Bookings
                    .Include(b => b.Payments)
                    .FirstOrDefault(b => b.booking_id == bookingId);

                if (booking != null)
                {
                    db.Payments.Add(new Payments
                    {
                        booking_id = bookingId,
                        payment_method = "VNPAY",
                        payment_kind = "deposit",
                        amount = amount,
                        status = "PAID",
                        paid_at = DateTime.Now,
                        transaction_id = transNo
                    });

                    booking.status = "Đã thanh toán";
                    db.SaveChanges();
                }

                return RedirectToAction("HoanTat", new { bookingId });
            }

            ViewBag.Message = "Thanh toán thất bại. Mã lỗi: " + responseCode;
            return View("PaymentError");
        }

        // ===================== 5) VNPAY IPN (OPTIONAL) =====================
        [HttpGet]
        public ActionResult VnpayIpn()
        {
            return Content("00");
        }

        // ===================== 6) HOÀN TẤT =====================
        [HttpGet]
        public ActionResult HoanTat(int bookingId)
        {
            if (bookingId <= 0)
                return RedirectToAction("Index", "Home");

            var booking = db.Bookings
                .AsNoTracking()
                .Include(b => b.Restaurants)
                .Include(b => b.Users)
                .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            int guests = (booking.number_of_guests <= 0) ? 1 : booking.number_of_guests;
            decimal deposit = guests * DEPOSIT_PER_GUEST;

            var pre = BuildPreOrderInfo(bookingId);
            decimal total = deposit + pre.Total;

            // Lấy payment mới nhất của booking
            var latestPay = db.Payments
                .AsNoTracking()
                .Where(p => p.booking_id == bookingId)
                .OrderByDescending(p => p.payment_id)
                .FirstOrDefault();

            string paymentMethod = DetectPaymentMethod(latestPay);
            string status = BuildDisplayStatus(booking.status, latestPay);

            decimal amountToShow = total;
            if (latestPay != null)
            {
                try
                {
                    var amt = Convert.ToDecimal(latestPay.amount);
                    if (amt > 0m) amountToShow = amt;
                }
                catch { }
            }

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,

                RestaurantId = booking.restaurant_id ?? 0,
                RestaurantName = booking.Restaurants?.name ?? "Đang cập nhật",
                RestaurantAddress = booking.Restaurants?.address ?? "",
                RestaurantImage = string.IsNullOrWhiteSpace(booking.Restaurants?.Image)
                    ? "https://via.placeholder.com/100"
                    : booking.Restaurants.Image,

                BookingTime = booking.booking_time,
                NumberOfGuests = guests,
                SpecialRequest = booking.special_request ?? "",

                DepositAmount = deposit,
                PreOrderTotal = pre.Total,
                PreOrderNote = pre.Note,
                PreOrderText = pre.Text,

                Amount = amountToShow,

                PaymentMethod = paymentMethod,
                Status = status,

                CustomerName = booking.Users?.full_name ?? "Khách",
                CustomerPhone = booking.Users?.phone ?? ""
            };

            return View(vm);
        }

        private string DetectPaymentMethod(Payments p)
        {
            if (p == null) return "Chưa chọn";
            var m = (p.payment_method ?? "").ToUpperInvariant();
            if (m.Contains("VNPAY")) return "VNPAY";
            if (m.Contains("BANK")) return "Chuyển khoản";
            return p.payment_method ?? "Đang cập nhật";
        }

        private string BuildDisplayStatus(string bookingStatus, Payments p)
        {
            var payStatus = (p?.status ?? "").ToLowerInvariant();
            var bookStatus = (bookingStatus ?? "").ToLowerInvariant();

            if (payStatus.Contains("paid") || payStatus.Contains("success") || payStatus.Contains("completed")
                || bookStatus.Contains("đã thanh toán") || bookStatus.Contains("thành công"))
                return "Thanh toán thành công";

            if (payStatus.Contains("pending") || payStatus.Contains("confirm")
                || bookStatus.Contains("chờ"))
                return "Chờ nhà hàng xác nhận";

            if (payStatus.Contains("fail") || payStatus.Contains("error") || payStatus.Contains("cancel")
                || bookStatus.Contains("hủy") || bookStatus.Contains("thất bại"))
                return "Thanh toán thất bại";

            return "Chờ xác nhận";
        }

        
        // ===================== PREORDER HELPERS ===================
      

        private class PreOrderInfo
        {
            public decimal Total { get; set; }
            public string Note { get; set; } 
            public string Text { get; set; } 
        }

        private PreOrderInfo BuildPreOrderInfo(int bookingId)
        {
            var rows = (from o in db.Orders.AsNoTracking()
                        join m in db.MenuItems.AsNoTracking()
                            on o.menu_item_id equals m.menu_item_id
                        where o.booking_id.HasValue && o.booking_id.Value == bookingId
                        select new
                        {
                            Name = m.name,
                            Qty = o.quantity,
                            Price = m.price
                        }).ToList();

            if (rows == null || rows.Count == 0)
                return new PreOrderInfo { Total = 0m, Note = null, Text = null };

            decimal total = 0m;
            var noteParts = new List<string>();
            var textLines = new List<string>();

            foreach (var r in rows)
            {
                int qty = (r.Qty <= 0) ? 1 : r.Qty;

                decimal unitPrice = 0m;
                try { unitPrice = Convert.ToDecimal(r.Price); } catch { unitPrice = 0m; }

                decimal lineTotal = unitPrice * qty;
                total += lineTotal;

                string name = string.IsNullOrWhiteSpace(r.Name) ? "Món" : r.Name.Trim();

                noteParts.Add($"{name} x{qty}");
                textLines.Add($"• {name} x{qty} — {lineTotal:#,##0} đ");
            }

            return new PreOrderInfo
            {
                Total = total,
                Note = string.Join("; ", noteParts),
                Text = string.Join("\n", textLines)
            };
        }

        // ===================== SMALL UTILITIES ====================

        private ActionResult SafeRedirect(string returnUrl, string fallbackAction, string fallbackController)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(fallbackAction, fallbackController);
        }
    }
}
