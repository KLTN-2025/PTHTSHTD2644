using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using System.Configuration;
using SmartTable.Models;
using SmartTable.Models.ViewModels;
using SmartTable.Models.Vnpay;

namespace SmartTable.Controllers
{
    public class ThanhToanController : Controller
    {
        private readonly Entities db = new Entities();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ================== HÀM PHỤ: PARSE SPECIAL_REQUEST ==================
        
        private void ParseSpecialRequest(
            string special,
            out string userNote,
            out string preOrderText,
            out decimal preOrderTotal)
        {
            userNote = null;
            preOrderText = null;
            preOrderTotal = 0m;

            if (string.IsNullOrWhiteSpace(special))
                return;

            var keyPre = "Món chuẩn bị trước:";
            var keyTotal = "Tiền món ước tính:";

            int idxPre = special.IndexOf(keyPre, StringComparison.OrdinalIgnoreCase);
            int idxTotal = special.IndexOf(keyTotal, StringComparison.OrdinalIgnoreCase);

            //  Ghi chú khách (phần trước "Món chuẩn bị trước:")
            if (idxPre > 0)
            {
                userNote = special.Substring(0, idxPre).Trim();
                if (userNote.EndsWith("|"))
                    userNote = userNote.Substring(0, userNote.Length - 1).Trim();
            }
            else if (idxPre < 0)
            {
                userNote = special.Trim();
            }

            
            if (idxPre >= 0)
            {
                int start = idxPre + keyPre.Length;
                int end = special.Length;

                int pipe = special.IndexOf("|", start);
                if (pipe >= 0) end = pipe;

                if (idxTotal >= 0 && idxTotal > start && idxTotal < end)
                    end = idxTotal;

                preOrderText = special.Substring(start, end - start)
                                      .Trim().Trim('|').Trim();
            }

            if (idxTotal >= 0)
            {
                int start = idxTotal + keyTotal.Length;
                int end = special.IndexOf("|", start);
                if (end < 0) end = special.Length;

                var totalStr = special.Substring(start, end - start).Trim();
                totalStr = totalStr.Replace("đ", "")
                                   .Replace("Đ", "")
                                   .Replace(".", "")
                                   .Replace(",", "")
                                   .Trim();

                decimal parsed;
                if (decimal.TryParse(totalStr, out parsed))
                {
                    preOrderTotal = parsed;
                }
            }
        }

        [HttpGet]
        public ActionResult Checkout(int bookingId, decimal? preOrder = null, string preOrderNote = null)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn (bookingId).";
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

            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            decimal deposit = soKhach * 50000m;

            string userNote;
            string preOrderText;
            decimal preOrderTotal;
            ParseSpecialRequest(booking.special_request ?? "",
                                out userNote,
                                out preOrderText,
                                out preOrderTotal);

            if (preOrder.HasValue && preOrder.Value > 0)
            {
                preOrderTotal = preOrder.Value;
            }
            if (!string.IsNullOrWhiteSpace(preOrderNote))
            {
                preOrderText = preOrderNote;
            }

            decimal amount = deposit + preOrderTotal;

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantId = booking.restaurant_id ?? 0,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                RestaurantAddress = booking.Restaurants != null ? booking.Restaurants.address : null,
                RestaurantImage = booking.Restaurants != null ? booking.Restaurants.Image : null,

                BookingTime = booking.booking_time,
                NumberOfGuests = soKhach,

                SpecialRequest = userNote,

                DepositAmount = deposit,
                PreOrderTotal = preOrderTotal,
                PreOrderText = preOrderText,
                PreOrderNote = preOrderText,

                Amount = amount,

                PaymentMethod = "Chuyển khoản",
                Status = "Pending",

                CustomerName = booking.Users != null ? booking.Users.full_name : null,
                CustomerPhone = booking.Users != null ? booking.Users.phone : null,
                TransferContent = $"SMARTTABLE-{booking.booking_id}",

                BankId = "MB",
                AccountNo = "0941326591",
                AccountName = "SMART TABLE"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(PaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Checkout", new { bookingId = model.BookingId });
            }

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Users)
                            .FirstOrDefault(b => b.booking_id == model.BookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            decimal deposit = soKhach * 50000m;

            decimal amount = model.Amount > 0 ? model.Amount : deposit;

            var payment = new Payments
            {
                booking_id = booking.booking_id,
                amount = amount,
                payment_method = string.IsNullOrEmpty(model.PaymentMethod)
                                    ? "Chuyển khoản"
                                    : model.PaymentMethod,
                status = "Đã thanh toán (CK thường)",
                transaction_id = Guid.NewGuid().ToString()
            };

            db.Payments.Add(payment);

            try
            {
                if (string.IsNullOrEmpty(booking.status) ||
                    booking.status == "Chờ thanh toán" ||
                    booking.status == "Đã đặt")
                {
                    booking.status = "Đã thanh toán";
                }
            }
            catch
            {

            }

            db.SaveChanges();

            return RedirectToAction("HoanTat", new { bookingId = booking.booking_id });
        }

        [HttpPost]
        public ActionResult ConfirmPayment(int bookingId)
        {
            var booking = db.Bookings
                            .Include(b => b.Payments)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin đặt bàn." });
            }

            var payment = booking.Payments
                                 .OrderByDescending(p => p.payment_id)
                                 .FirstOrDefault();

            if (payment != null)
            {
                payment.status = "Đã thanh toán";
            }

            try
            {
                booking.status = "Đã thanh toán";
            }
            catch { }

            db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public ActionResult HoanTat(int bookingId)
        {
            if (bookingId <= 0)
            {
                TempData["ErrorMessage"] = "Thiếu mã đặt bàn (bookingId).";
                return RedirectToAction("Index", "Home");
            }

            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .Include(b => b.Payments)
                            .Include(b => b.Users)
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var payment = booking.Payments
                                 .OrderByDescending(p => p.payment_id)
                                 .FirstOrDefault();

            var soKhach = booking.number_of_guests;
            if (soKhach <= 0) soKhach = 1;

            decimal deposit = soKhach * 50000m;
            decimal amount = payment != null ? payment.amount : deposit;

            string userNote;
            string preOrderText;
            decimal preOrderTotal;
            ParseSpecialRequest(booking.special_request ?? "",
                                out userNote,
                                out preOrderText,
                                out preOrderTotal);

            var vm = new PaymentViewModel
            {
                BookingId = booking.booking_id,
                RestaurantId = booking.restaurant_id ?? 0,
                RestaurantName = booking.Restaurants != null ? booking.Restaurants.name : "Nhà hàng",
                RestaurantAddress = booking.Restaurants != null ? booking.Restaurants.address : null,
                RestaurantImage = booking.Restaurants != null ? booking.Restaurants.Image : null,

                BookingTime = booking.booking_time,
                NumberOfGuests = soKhach,
                SpecialRequest = userNote,

                Amount = amount,
                DepositAmount = deposit,
                PreOrderTotal = preOrderTotal,
                PreOrderText = preOrderText,
                PreOrderNote = preOrderText,

                PaymentMethod = payment != null ? payment.payment_method : null,
                Status = payment != null ? payment.status : "Chờ xác nhận",

                CustomerName = booking.Users != null ? booking.Users.full_name : null,
                CustomerPhone = booking.Users != null ? booking.Users.phone : null,
                TransferContent = $"SMARTTABLE-{booking.booking_id}"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PayWithVnPay(int bookingId, decimal amount)
        {
            var booking = db.Bookings
                            .Include("Restaurants")
                            .Include("Users")
                            .FirstOrDefault(b => b.booking_id == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("Index", "Home");
            }

            var soKhach = booking.number_of_guests <= 0 ? 1 : booking.number_of_guests;
            var defaultDeposit = soKhach * 50000m;

            var amountToPay = amount > 0 ? amount : defaultDeposit;

            var paymentModel = new PaymentInformationModel
            {
                booking_id = booking.booking_id,
                OrderType = "other",
                Amount = (double)amountToPay,
                OrderDescription = $"Thanh toán cọc đặt bàn #{booking.booking_id}",
                Name = booking.Users != null ? booking.Users.full_name : "Khách hàng"
            };

            var vnp_Url = ConfigurationManager.AppSettings["vnp_Url"];
            var vnp_TmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"];
            var vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
            var vnp_ReturnUrl = ConfigurationManager.AppSettings["vnp_ReturnUrl"];

            var vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", ((long)(paymentModel.Amount * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", vnpay.GetIpAddress(Request));
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", paymentModel.OrderDescription);
            vnpay.AddRequestData("vnp_OrderType", paymentModel.OrderType);
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);
            vnpay.AddRequestData("vnp_TxnRef", booking.booking_id.ToString());

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

            return Redirect(paymentUrl);
        }

        [HttpGet]
        public ActionResult PaymentCallbackVnpay()
        {
            var vnpay = new VnPayLibrary();

            // Lấy toàn bộ query vnp_***
            foreach (string key in Request.QueryString.Keys)
            {
                string value = Request.QueryString[key];
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value);
                }
            }

            string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
            string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];

            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");
            string amountStr = vnpay.GetResponseData("vnp_Amount");

            decimal amount = 0;
            if (!string.IsNullOrEmpty(amountStr) && long.TryParse(amountStr, out var amountRaw))
            {
                amount = amountRaw / 100m;   
            }

            int bookingId = 0;
            int.TryParse(vnp_TxnRef, out bookingId);

            if (!checkSignature)
            {
                ViewBag.Message = "Sai chữ ký (checksum).";
                return View("PaymentError");
            }

            if (vnp_ResponseCode == "00") 
            {
                var booking = db.Bookings
                                .Include("Payments")
                                .FirstOrDefault(b => b.booking_id == bookingId);

                if (booking != null)
                {
                    var payment = new Payments
                    {
                        booking_id = bookingId,
                        amount = amount,
                        payment_method = "VNPAY",
                        status = "Đã thanh toán (VNPAY)",
                        transaction_id = vnpay.GetResponseData("vnp_TransactionNo")
                    };

                    db.Payments.Add(payment);
                    booking.status = "Đã thanh toán";
                    db.SaveChanges();
                }

                return RedirectToAction("HoanTat", new { bookingId = bookingId });
            }
            else
            {
                ViewBag.Message = "Thanh toán thất bại. Mã lỗi: " + vnp_ResponseCode;
                return View("PaymentError");
            }
        }

        [HttpGet]
        public ActionResult VnpayIpn()
        {
            var vnpay = new VnPayLibrary();

            foreach (string key in Request.QueryString.Keys)
            {
                string value = Request.QueryString[key];
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value);
                }
            }

            string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
            string vnp_SecureHash = Request.QueryString["vnp_SecureHash"];

            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);
            if (!checkSignature)
            {
                return Content("97"); 
            }

            string vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_Amount = vnpay.GetResponseData("vnp_Amount");

            int bookingId;
            int.TryParse(vnp_TxnRef, out bookingId);

            if (vnp_ResponseCode == "00")
            {
                var booking = db.Bookings
                                .Include("Payments")
                                .FirstOrDefault(b => b.booking_id == bookingId);
                if (booking != null)
                {
                    decimal amount = 0;
                    if (!string.IsNullOrEmpty(vnp_Amount) && long.TryParse(vnp_Amount, out var raw))
                    {
                        amount = raw / 100m;
                    }

                    db.Payments.Add(new Payments
                    {
                        booking_id = bookingId,
                        amount = amount,
                        payment_method = "VNPAY-IPN",
                        status = "Đã thanh toán (IPN)",
                        transaction_id = vnpay.GetResponseData("vnp_TransactionNo")
                    });

                    booking.status = "Đã thanh toán (IPN)";
                    db.SaveChanges();
                }

                return Content("00");
            }

            return Content("02");
        }
    }
}
