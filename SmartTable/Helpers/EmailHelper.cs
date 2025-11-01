using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

// Namespace này (SmartTable.Helpers) sẽ khớp với 'using' trong Controller của bạn
namespace SmartTable.Helpers
{
    public static class EmailHelper
    {
        public static void SendEmail(string toEmail, string subject, string body)
        {
            // Đọc thông tin từ Web.config
            var fromEmail = ConfigurationManager.AppSettings["FromEmailAddress"];
            var fromPassword = ConfigurationManager.AppSettings["FromEmailPassword"];
            var displayName = ConfigurationManager.AppSettings["FromEmailDisplayName"];

            // Dự phòng nếu Web.config thiếu (dùng Mật khẩu ứng dụng mới nhất bạn cung cấp)
            if (string.IsNullOrEmpty(fromPassword))
            {
                fromPassword = "rpxrrencvhiekcxf"; // Mật khẩu App mới nhất của bạn
            }
            if (string.IsNullOrEmpty(fromEmail))
            {
                fromEmail = "phamhuynhduyphong0308@gmail.com";
            }
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = "Smart Table";
            }

            try
            {
                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromEmail, fromPassword)
                };

                var fromAddress = new MailAddress(fromEmail, displayName);
                var toAddress = new MailAddress(toEmail);

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi để debug
                System.Diagnostics.Debug.WriteLine("LỖI GỬI EMAIL HELPER: " + ex.Message);
                // Ném lỗi ra để Controller bắt được
                throw;
            }
        }
    }
}