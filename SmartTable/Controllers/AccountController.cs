using SmartTable.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Configuration; // Thêm
using System.Net.Mail; // Thêm
using SmartTable.Filters; // Thêm (nếu bạn dùng AuthorizeUser)
using BCrypt.Net; // Thêm BCrypt

namespace SmartTable.Controllers
{
    public class AccountController : Controller
    {
        private Entities db = new Entities(); // Giả sử DbContext của bạn tên là Entities

        // --- ĐĂNG KÝ ---
        [HttpGet]
        public ActionResult Register()
        {
            return View(new Users());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Users model)
        {
            // (Validation thủ công)
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

            // Kiểm tra email trùng
            if (db.Users.Any(u => u.email == model.email))
            {
                ModelState.AddModelError("email", "Email đã được sử dụng.");
            }

            if (ModelState.IsValid)
            {
                // Băm mật khẩu
                model.password_hash = BCrypt.Net.BCrypt.HashPassword(model.password_hash);
                model.role = "User";
                model.created_at = DateTime.Now;

                db.Users.Add(model);
                db.SaveChanges();

                TempData["RegisterSuccess"] = "Đăng ký thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login", "Account");
            }

            return View(model);
        }

        // --- ĐĂNG NHẬP ---
        [HttpGet]
        public ActionResult Login()
        {
            return View(new Users());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(Users model)
        {
            var user = db.Users.FirstOrDefault(u => u.email == model.email);

            // So sánh mật khẩu đã băm
            if (user != null && BCrypt.Net.BCrypt.Verify(model.password_hash, user.password_hash))
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
                    return RedirectToAction("Index", "BusinessHome");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // --- ĐĂNG XUẤT ---
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // --- THÔNG TIN TÀI KHOẢN (Chuyển hướng) ---
        [AuthorizeUser]
        public ActionResult AccountInfo()
        {
            // Trang này giờ chính là trang UpdateAccount
            return RedirectToAction("UpdateAccount");
        }

        // --- CẬP NHẬT TÀI KHOẢN (GET) ---
        [AuthorizeUser]
        [HttpGet]
        public ActionResult UpdateAccount()
        {
            if (Session["user_id"] == null) return RedirectToAction("Login");
            var userId = (int)Session["user_id"];

            var user = db.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
        }

        // --- CẬP NHẬT TÀI KHOẢN (POST) ---
        [AuthorizeUser]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateAccount(Users model, string newPassword, string confirmPassword)
        {
            if (Session["user_id"] == null) return RedirectToAction("Login");
            var userId = (int)Session["user_id"];

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
                    user.full_name = model.full_name;
                }

                // Cập nhật email (Nếu bạn cho phép)
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
                    user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                }

                db.SaveChanges();
                ViewBag.SuccessMessage = "Cập nhật thông tin tài khoản thành công.";
                // Sửa: Trả về View() để hiển thị thông báo thành công
                return View("UpdateAccount", user);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi cập nhật thông tin: " + ex.Message;
                return View("UpdateAccount", user);
            }
        }

        // --- QUÊN MẬT KHẨU (GET) ---
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // --- QUÊN MẬT KHẨU (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(Users model)
        {
            if (!string.IsNullOrEmpty(model.email))
            {
                var user = db.Users.FirstOrDefault(u => u.email == model.email);
                if (user != null)
                {
                    try
                    {
                        string newPassword = GenerateRandomPassword();
                        user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        db.SaveChanges();

                        string subject = "Mật khẩu mới từ hệ thống đặt bàn";
                        string body = $"Chào {user.full_name},\n\nMật khẩu mới của bạn là: {newPassword}\n" +
                                      $"Vui lòng đăng nhập và đổi mật khẩu sau khi đăng nhập.\n\nTrân trọng.";
                        SendEmail(user.email, subject, body);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", "Không thể gửi email. Lỗi: " + ex.Message);
                        return View(model);
                    }
                }
                TempData["RegisterSuccess"] = "Nếu email của bạn tồn tại, một mật khẩu mới sẽ được gửi.";
                return RedirectToAction("Login", "Account");
            }

            TempData["Message"] = "Email không hợp lệ.";
            return View(model);
        }

        // --- ĐỔI MẬT KHẨU (GET) --- (ĐÃ DI CHUYỂN LÊN TRÊN)
        [AuthorizeUser]
        [HttpGet]
        public ActionResult ChangePassword()
        {
            return View();
        }

        // --- ĐỔI MẬT KHẨU (POST) --- (ĐÃ DI CHUYỂN LÊN TRÊN)
        [AuthorizeUser]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (Session["user_id"] == null) return RedirectToAction("Login");
            var userId = (int)Session["user_id"];
            var user = db.Users.Find(userId);

            if (user == null) return HttpNotFound();

            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.password_hash))
            {
                ViewBag.ErrorMessage = "Mật khẩu cũ không chính xác.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return View();
            }

            user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            db.SaveChanges();

            ViewBag.SuccessMessage = "Đổi mật khẩu thành công!";
            return View();
        }


        // --- HÀM HỖ TRỢ ---
        private string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            var fromEmail = ConfigurationManager.AppSettings["FromEmailAddress"];
            var fromPassword = ConfigurationManager.AppSettings["FromEmailPassword"];
            var displayName = ConfigurationManager.AppSettings["FromEmailDisplayName"];

            if (string.IsNullOrEmpty(fromPassword))
            {
                fromPassword = "rpxrrencvhiekcxf"; // Mật khẩu App mới
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
                    Credentials = new System.Net.NetworkCredential(fromEmail, fromPassword)
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
                System.Diagnostics.Debug.WriteLine("LỖI GỬI EMAIL: " + ex.Message);
                throw;
            }
        }

        // Ghi đè phương thức Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

    } // <-- Đóng class AccountController
} // <-- Đóng namespace SmartTable.Controllers