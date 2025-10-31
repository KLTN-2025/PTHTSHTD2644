using SmartTable.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Configuration; // Thêm
using System.Net.Mail; // Thêm
using SmartTable.Filters; // Thêm (nếu bạn dùng AuthorizeUser)

namespace SmartTable.Controllers
{
    public class AccountController : Controller
    {
        private Entities db = new Entities(); // Giả sử DbContext của bạn tên là Entities

        // --- ĐĂNG KÝ ---
        [HttpGet]
        public ActionResult Register()
        {
            return View(new Users()); // Sử dụng Users (số nhiều) nếu đó là tên lớp Model của bạn
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Users model)
        {
            // Kiểm tra validation thủ công (Nên dùng Data Annotations trên Model)
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
                // Băm mật khẩu trước khi lưu (BẮT BUỘC BẢO MẬT)
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
            // TÌM USER BẰNG EMAIL
            var user = db.Users.FirstOrDefault(u => u.email == model.email);

            // KIỂM TRA MẬT KHẨU ĐÃ BĂM
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

            // Chỉ trả về một lỗi chung chung
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // --- ĐĂNG XUẤT ---
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        // --- THÔNG TIN TÀI KHOẢN ---
        [AuthorizeUser] // <-- Dùng Filter (nếu có) hoặc kiểm tra Session
        public ActionResult AccountInfo()
        {
            if (Session["user_id"] == null) return RedirectToAction("Login"); // Kiểm tra thủ công nếu không dùng Filter
            var userId = (int)Session["user_id"];

            var user = db.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            return View(user);
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
                    if (!System.Text.RegularExpressions.Regex.IsMatch(model.phone, @"^\+?\d{10,15}$")) // Nới lỏng Regex
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
                    // Băm mật khẩu mới
                    user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
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

        // --- QUÊN MẬT KHẨU (GET) ---
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        // --- QUÊN MẬT KHẨU (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(Users model) // Dùng Model để lấy Email
        {
            if (!string.IsNullOrEmpty(model.email))
            {
                var user = db.Users.FirstOrDefault(u => u.email == model.email);
                if (user != null)
                {
                    try
                    {
                        // Tạo mật khẩu mới
                        string newPassword = GenerateRandomPassword();
                        // Băm mật khẩu mới
                        user.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        db.SaveChanges();

                        // Gửi email MẬT KHẨU GỐC
                        string subject = "Mật khẩu mới từ hệ thống đặt bàn";
                        string body = $"Chào {user.full_name},\n\nMật khẩu mới của bạn là: {newPassword}\n" +
                                      $"Vui lòng đăng nhập và đổi mật khẩu sau khi đăng nhập.\n\nTrân trọng.";
                        SendEmail(user.email, subject, body);
                    }
                    catch (Exception ex)
                    {
                        // Hiển thị lỗi nếu gửi mail thất bại
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
            // Đọc từ Web.config
            var fromEmail = ConfigurationManager.AppSettings["FromEmailAddress"];
            var fromPassword = ConfigurationManager.AppSettings["FromEmailPassword"];
            var displayName = ConfigurationManager.AppSettings["FromEmailDisplayName"];

            // Mật khẩu MỚI (đúng)
            if (string.IsNullOrEmpty(fromPassword))
            {
                fromPassword = "bjtpwkkuqllpllzo"; // Thay thế bằng mật khẩu App mới
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
                throw; // Ném lỗi ra để action ForgotPassword bắt được
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