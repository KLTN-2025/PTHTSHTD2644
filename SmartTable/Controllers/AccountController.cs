using SmartTable.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SmartTable.Controllers
{
    public class AccountController : Controller
    {
        private SmartTableEntities db = new SmartTableEntities();

        [HttpGet]
        public ActionResult Register()
        {
            return View(new User());
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User model)
        {
            if (string.IsNullOrEmpty(model.email))
                ModelState.AddModelError("email", "Email không được để trống.");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(model.email, @"^[a-zA-Z0-9._%+-]+@gmail\.com$"))
                ModelState.AddModelError("email", "Email phải đúng định dạng và kết thúc bằng @gmail.com.");


            if (string.IsNullOrEmpty(model.phone))
                ModelState.AddModelError("phone", "Số điện thoại không được để trống.");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(model.phone, @"^0\d{9,10}$"))
                ModelState.AddModelError("phone", "Số điện thoại phải bắt đầu bằng 0 và có 10 hoặc 11 số.");
            if (string.IsNullOrEmpty(model.password_hash))
                ModelState.AddModelError("password_hash", "Mật khẩu không được để trống.");
            if (string.IsNullOrEmpty(model.full_name))
                ModelState.AddModelError("full_name", " không được để trống.");

            if (ModelState.IsValid)
            {
                if (db.Users.Any(u => u.email == model.email))
                {
                    ModelState.AddModelError("email", "Email đã được sử dụng.");
                    return View(model);
                }

                model.role = "User";
                model.created_at = DateTime.Now;

                db.Users.Add(model);
                db.SaveChanges();

                TempData["RegisterSuccess"] = "Đăng ký thành công! Vui lòng đăng nhập.";

                return RedirectToAction("Login", "Account");
            }

            return View(model);
        }

        [HttpGet]
        public ActionResult Login()
        {
            return View(new User());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(User model)
        {
            if (ModelState.IsValid)
            {
                var user = db.Users.FirstOrDefault(u => u.email == model.email && u.password_hash == model.password_hash);
                if (user != null)
                {
                    Session["user"] = user;
                    Session["user_id"] = user.user_id;
                    Session["role"] = user.role;

                    if (user.role == "Admin")
                    {
                        return RedirectToAction("Index", "Role", new { area = "Admin" });
                    }
                    else if (user.role == "business")
                    {
                        // Chuyển hướng đến BusinessHomeController
                        return RedirectToAction("Index", "BusinessHome");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            }

            return View(model);
        }


        public ActionResult AccountInfo()
        {
            var userId = Session["user_id"] != null ? (int)Session["user_id"] : 0;
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = db.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        [HttpGet]
        public ActionResult UpdateAccount()
        {
            var userId = Session["user_id"] != null ? (int)Session["user_id"] : 0;
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = db.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateAccount(User model, string newPassword, string confirmPassword)
        {
            var userId = Session["user_id"] != null ? (int)Session["user_id"] : 0;
            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = db.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            try
            {
                // Cập nhật họ và tên
                if (!string.IsNullOrEmpty(model.full_name) && model.full_name != user.full_name)
                {
                    if (model.full_name.Length > 100)
                    {
                        ViewBag.ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.";
                        return View("UpdateAccount", user);
                    }
                    user.full_name = model.full_name;
                }

                // Cập nhật email
                if (!string.IsNullOrEmpty(model.email) && model.email != user.email)
                {
                    if (db.Users.Any(u => u.email == model.email && u.user_id != userId))
                    {
                        ViewBag.ErrorMessage = "Email đã được sử dụng.";
                        return View("UpdateAccount", user);
                    }
                    user.email = model.email;
                }

                // Cập nhật số điện thoại
                if (!string.IsNullOrEmpty(model.phone) && model.phone != user.phone)
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(model.phone, @"^\+?\d{10,15}$"))
                    {
                        ViewBag.ErrorMessage = "Số điện thoại không hợp lệ.";
                        return View("UpdateAccount", user);
                    }
                    user.phone = model.phone;
                }

                // Cập nhật mật khẩu
                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (newPassword != confirmPassword)
                    {
                        ViewBag.PasswordError = "Mật khẩu xác nhận không khớp.";
                        return View("UpdateAccount", user);
                    }
                    if (newPassword.Length < 6)
                    {
                        ViewBag.PasswordError = "Mật khẩu phải có ít nhất 6 ký tự.";
                        return View("UpdateAccount", user);
                    }
                    user.password_hash = newPassword;
                }

                db.SaveChanges();
                ViewBag.SuccessMessage = "Cập nhật thông tin tài khoản thành công.";
                return RedirectToAction("AccountInfo");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi cập nhật thông tin: " + ex.Message;
                return View("UpdateAccount", user);
            }
        }
        // quên mk
        public ActionResult ForgotPassword()
        {
            return View();
        }
        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]

        public ActionResult ForgotPassword(User model)
        {
            if (!string.IsNullOrEmpty(model.email))
            {
                // ✅ Kiểm tra định dạng email
                if (!System.Text.RegularExpressions.Regex.IsMatch(model.email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    TempData["Message"] = "Định dạng email không hợp lệ.";
                    return View();
                }

                var user = db.Users.FirstOrDefault(u => u.email == model.email);
                if (user != null)
                {
                    // Tạo mật khẩu mới
                    string newPassword = GenerateRandomPassword();
                    user.password_hash = newPassword;

                    db.SaveChanges();

                    // Gửi email
                    string subject = "Mật khẩu mới từ hệ thống đặt bàn";
                    string body = $"Chào {user.full_name},\n\nMật khẩu mới của bạn là: {newPassword}\n" +
                                  $"Vui lòng đăng nhập và đổi mật khẩu sau khi đăng nhập.\n\nTrân trọng.";

                    SendEmail(user.email, subject, body);
                }

                TempData["RegisterSuccess"] = "Vui lòng kiểm tra email của bạn để nhận mật khẩu mới.";
                return RedirectToAction("Login", "Account");
            }

            TempData["Message"] = "Email không hợp lệ.";
            return View();
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            var fromEmail = "mymy12345phong@gmail.com";
            var fromPassword = "tisyqyhjhljtiopk"; // App password

            var smtp = new System.Net.Mail.SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(fromEmail, fromPassword)
            };

            // ✅ Thêm tên người gửi ở đây
            var fromAddress = new System.Net.Mail.MailAddress(fromEmail, "BookingRestaurantSystem");
            var toAddress = new System.Net.Mail.MailAddress(toEmail);

            using (var message = new System.Net.Mail.MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                smtp.Send(message);
            }
        }


    }
}


