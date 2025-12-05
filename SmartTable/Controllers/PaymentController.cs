using SmartTable.Models;
using SmartTable.Models.Vnpay;
using SmartTable.Services;
using System;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    public class PaymentController : Controller
    {
        private readonly Entities db = new Entities();
        private readonly IVnPayService _vnPayService;

        public PaymentController()
            : this(new VnPayService())
        {
        }

        public PaymentController(IVnPayService vnPayService)
        {
            _vnPayService = vnPayService;
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePaymentUrlVnpay(int bookingId, double amount, string description)
        {
            var booking = db.Bookings.Find(bookingId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin đặt bàn.";
                return RedirectToAction("History", "Booking"); 
            }

            var model = new PaymentInformationModel
            {
                booking_id = bookingId,
                Amount = amount,
                OrderDescription = description,
                OrderType = "billpayment",
                Name = booking.Users != null ? booking.Users.full_name : "Khách"
            };

            var url = _vnPayService.CreatePaymentUrl(model, Request);
            return Redirect(url); 
        }

       
        [HttpGet]
        public ActionResult PaymentCallbackVnpay()
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

            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");
            string amountStr = vnpay.GetResponseData("vnp_Amount");

            decimal amount = 0;
            if (!string.IsNullOrEmpty(amountStr) && long.TryParse(amountStr, out var raw))
            {
                amount = raw / 100m;
            }

            int bookingId;
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
                    db.Payments.Add(new Payments
                    {
                        booking_id = bookingId,
                        amount = amount,
                        payment_method = "VNPAY",
                        status = "Đã thanh toán (VNPAY)",
                        transaction_id = vnpay.GetResponseData("vnp_TransactionNo")
                    });

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
