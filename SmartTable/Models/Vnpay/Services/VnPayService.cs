using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Web;
using SmartTable.Models.Vnpay;

namespace SmartTable.Services
{
    public class VnPayService : IVnPayService
    {
        private readonly string _tmnCode;
        private readonly string _hashSecret;
        private readonly string _baseUrl;
        private readonly string _version;
        private readonly string _command;
        private readonly string _currCode;
        private readonly string _locale;
        private readonly string _returnUrl;

        public VnPayService()
        {
            _tmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"];
            _hashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];
            _baseUrl = ConfigurationManager.AppSettings["vnp_Url"];
            _returnUrl = ConfigurationManager.AppSettings["vnp_ReturnUrl"];

            _version = ConfigurationManager.AppSettings["vnp_Version"] ?? "2.1.0";
            _command = ConfigurationManager.AppSettings["vnp_Command"] ?? "pay";
            _currCode = ConfigurationManager.AppSettings["vnp_CurrCode"] ?? "VND";
            _locale = ConfigurationManager.AppSettings["vnp_Locale"] ?? "vn";
        }

        public string CreatePaymentUrl(PaymentInformationModel model, HttpRequestBase request)
        {
            var vnPay = new VnPayLibrary();

            var orderId = model.booking_id.ToString();
            var createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

            vnPay.AddRequestData("vnp_Version", _version);
            vnPay.AddRequestData("vnp_Command", _command);
            vnPay.AddRequestData("vnp_TmnCode", _tmnCode);
            vnPay.AddRequestData("vnp_Amount", ((long)model.Amount * 100).ToString());
            vnPay.AddRequestData("vnp_CreateDate", createDate);
            vnPay.AddRequestData("vnp_CurrCode", _currCode);

            vnPay.AddRequestData("vnp_IpAddr", vnPay.GetIpAddress(request));

            vnPay.AddRequestData("vnp_Locale", _locale);
            vnPay.AddRequestData("vnp_OrderInfo", model.OrderDescription);
            vnPay.AddRequestData("vnp_OrderType", model.OrderType);
            vnPay.AddRequestData("vnp_ReturnUrl", _returnUrl);
            vnPay.AddRequestData("vnp_TxnRef", orderId);

            var paymentUrl = vnPay.CreateRequestUrl(_baseUrl, _hashSecret);
            return paymentUrl;
        }


        public PaymentResponseModel PaymentExecute(NameValueCollection vnpayData)
        {
            var pay = new VnPayLibrary();
            var response = pay.GetFullResponseData(vnpayData, _hashSecret);
            return response;
        }
    }
}
