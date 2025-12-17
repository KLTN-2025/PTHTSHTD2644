using System;

namespace SmartTable.Models.Vnpay
{
    /// <summary>
    /// Thông tin đơn thanh toán gửi sang VNPAY
    /// </summary>
    public class PaymentInformationModel
    {
        // Để map với Booking trong hệ thống của bạn
        public int booking_id { get; set; }

        // Loại đơn hàng (billpayment, other...)
        public string OrderType { get; set; }

        // Số tiền thanh toán (VNĐ)
        public double Amount { get; set; }

        // Mô tả đơn hàng
        public string OrderDescription { get; set; }

        // Tên người thanh toán
        public string Name { get; set; }
        public string TxnRef { get; set; }

    }
}
