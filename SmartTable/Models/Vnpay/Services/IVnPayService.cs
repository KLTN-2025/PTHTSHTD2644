using System.Collections.Specialized;
using System.Web;
using SmartTable.Models.Vnpay;

namespace SmartTable.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(PaymentInformationModel model, HttpRequestBase request);
        PaymentResponseModel PaymentExecute(NameValueCollection vnpayData);
    }
}
