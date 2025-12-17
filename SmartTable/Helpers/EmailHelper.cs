using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace SmartTable.Helpers
{
    public static class EmailHelper
    {
        public static void SendEmail(string toEmail, string subject, string body)
        {
            var fromEmail = ConfigurationManager.AppSettings["FromEmailAddress"];
            var fromPassword = ConfigurationManager.AppSettings["FromEmailPassword"];
            var displayName = ConfigurationManager.AppSettings["FromEmailDisplayName"];

            if (string.IsNullOrEmpty(fromPassword))
            {
                fromPassword = "wxzchziuhthubgha"; 
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
                System.Diagnostics.Debug.WriteLine("LỖI GỬI EMAIL HELPER: " + ex.Message);
                throw; 
            }
        }
    }
}