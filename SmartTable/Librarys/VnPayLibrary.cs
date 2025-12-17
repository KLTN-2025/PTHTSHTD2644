using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace SmartTable.Models.Vnpay
{
    public class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData =
            new SortedList<string, string>(new VnPayCompare());

        private readonly SortedList<string, string> _responseData =
            new SortedList<string, string>(new VnPayCompare());


        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                _requestData[key] = value;
            }
        }

        public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
        {
            var data = new StringBuilder();

            foreach (var kv in _requestData)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;

                data.Append(WebUtility.UrlEncode(kv.Key));
                data.Append("=");
                data.Append(WebUtility.UrlEncode(kv.Value));
                data.Append("&");
            }

            if (data.Length > 0)
                data.Length -= 1; 

            var rawData = data.ToString();
            var secureHash = HmacSha512(vnpHashSecret, rawData);

            var url = $"{baseUrl}?{rawData}&vnp_SecureHash={secureHash}";

            System.Diagnostics.Debug.WriteLine("=== VNPAY REQUEST ===");
            System.Diagnostics.Debug.WriteLine("RawData  : " + rawData);
            System.Diagnostics.Debug.WriteLine("Hash     : " + secureHash);
            System.Diagnostics.Debug.WriteLine("FinalURL : " + url);

            return url;
        }


        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                _responseData[key] = value;
            }
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var ret)
                ? ret
                : string.Empty;
        }

        public bool ValidateSignature(string inputHash, string secretKey)
        {
            var raw = GetResponseRawData();
            var myHash = HmacSha512(secretKey, raw);

            System.Diagnostics.Debug.WriteLine("=== VNPAY RESPONSE ===");
            System.Diagnostics.Debug.WriteLine("RawData    : " + raw);
            System.Diagnostics.Debug.WriteLine("VNPAY Hash : " + inputHash);
            System.Diagnostics.Debug.WriteLine("Our  Hash  : " + myHash);

            return string.Equals(myHash, inputHash,
                StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetResponseRawData()
        {
            var data = new StringBuilder();

            foreach (var kv in _responseData)
            {
                if (kv.Key == "vnp_SecureHash" || kv.Key == "vnp_SecureHashType")
                    continue;
                if (string.IsNullOrEmpty(kv.Value))
                    continue;

                data.Append(WebUtility.UrlEncode(kv.Key));
                data.Append("=");
                data.Append(WebUtility.UrlEncode(kv.Value));
                data.Append("&");
            }

            if (data.Length > 0)
                data.Length -= 1; 

            return data.ToString();
        }

        private static string HmacSha512(string key, string inputData)
        {
            var safeKey = (key ?? string.Empty).Trim();

            var keyBytes = Encoding.UTF8.GetBytes(safeKey);
            var inputBytes = Encoding.UTF8.GetBytes(inputData ?? string.Empty);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(inputBytes);
                var sb = new StringBuilder(hashBytes.Length * 2);
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public string GetIpAddress(HttpRequestBase request)
        {
            try
            {
                if (request == null) return "127.0.0.1";

                var ipAddress = request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    var addresses = ipAddress.Split(',');
                    if (addresses.Length > 0)
                        ipAddress = addresses[0].Trim();
                }
                else
                {
                    ipAddress = request.UserHostAddress;
                }

                if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1" || ipAddress.Contains(":"))
                {
                    ipAddress = "127.0.0.1";
                }

                return ipAddress;
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public PaymentResponseModel GetFullResponseData(NameValueCollection collection, string hashSecret)
        {
            var vnPay = new VnPayLibrary();

            foreach (string key in collection.Keys)
            {
                var value = collection[key];
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnPay.AddResponseData(key, value);
                }
            }

            var orderId = vnPay.GetResponseData("vnp_TxnRef");
            var vnPayTranId = vnPay.GetResponseData("vnp_TransactionNo");
            var vnpResponseCode = vnPay.GetResponseData("vnp_ResponseCode");
            var vnpSecureHash = collection["vnp_SecureHash"];
            var orderInfo = vnPay.GetResponseData("vnp_OrderInfo");

            var ok = vnPay.ValidateSignature(vnpSecureHash, hashSecret);
            if (!ok)
            {
                return new PaymentResponseModel { Success = false };
            }

            return new PaymentResponseModel
            {
                Success = true,
                PaymentMethod = "VNPAY",
                OrderDescription = orderInfo,
                OrderId = orderId,
                PaymentId = vnPayTranId,
                TransactionId = vnPayTranId,
                Token = vnpSecureHash,
                VnPayResponseCode = vnpResponseCode
            };
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var ci = CompareInfo.GetCompareInfo("en-US");
            return ci.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}