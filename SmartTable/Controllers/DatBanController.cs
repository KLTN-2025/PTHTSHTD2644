using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SmartTable.Models;

namespace SmartTable.Controllers
{
    public class DatBanController : Controller
    {
        private Entities db = new Entities();

        // ==================================================
        // 1. TRANG ĐẶT BÀN (GET)
        // ==================================================
        // [GET] Hiển thị trang đặt bàn
        [HttpGet]
        public ActionResult DatBan(int? id, string ReservationDate, int? NumberOfPeople)
        {
            // 1. KIỂM TRA ĐĂNG NHẬP
            if (Session["user"] == null)
            {
                // Lưu URL hiện tại để quay lại sau khi login
                Session["ReturnUrl"] = Request.Url.ToString();
                return RedirectToAction("Login", "Account");
            }

            if (id == null) return RedirectToAction("Index", "Home");

            var restaurant = db.Restaurants.Find(id);
            if (restaurant == null) return HttpNotFound();

            ViewBag.PreSelectedDate = !string.IsNullOrEmpty(ReservationDate) ? ReservationDate : DateTime.Now.ToString("yyyy-MM-dd");
            ViewBag.PreSelectedPeople = NumberOfPeople ?? 2;

            // Tự động điền thông tin user đã đăng nhập
            var user = (Users)Session["user"];
            ViewBag.UserFullName = user.full_name;
            ViewBag.UserPhone = user.phone;

            return View(restaurant);
        }

        // ==================================================
        // 2. XỬ LÝ LƯU ĐẶT BÀN (POST)
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LuuDatBan(
            int restaurant_id,
            string booking_time,      // Ngày (yyyy-MM-dd)
            string booking_time_hour, // Giờ (HH:mm)
            int number_of_guests,
            string full_name,
            string phone,
            string special_request)

        {
            // 1. KIỂM TRA ĐĂNG NHẬP
            if (Session["user"] == null)
            {
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
            }
            try
            {
                // 1. Ghép Ngày + Giờ thành DateTime
                DateTime finalBookingTime;
                try
                {
                    // Format string để parse: "yyyy-MM-dd HH:mm"
                    string timeString = $"{booking_time} {booking_time_hour}";
                    finalBookingTime = DateTime.Parse(timeString);
                }
                catch
                {
                    // Nếu lỗi format, dùng thời gian hiện tại + 1 tiếng
                    finalBookingTime = DateTime.Now.AddHours(1);
                }

                // 2. Kiểm tra Logic
                if (finalBookingTime < DateTime.Now)
                {
                    // Thông báo lỗi (có thể dùng TempData hoặc return View kèm error)
                    TempData["ErrorMessage"] = "Thời gian đặt bàn không hợp lệ (phải là tương lai).";
                    return RedirectToAction("DatBan", new { id = restaurant_id });
                }

                // 3. Lấy User ID (nếu có)
                int? userId = null;
                if (Session["user"] != null)
                {
                    userId = ((Users)Session["user"]).user_id;
                }

                // 4. Tạo đối tượng Booking
                var booking = new Bookings
                {
                    restaurant_id = restaurant_id,
                    user_id = userId,
                    booking_time = finalBookingTime,
                    number_of_guests = number_of_guests,
                    status = "Chờ xác nhận", // Trạng thái khởi tạo

                    // Lưu thông tin khách vào Note nếu DB chưa có cột riêng
                    // Hoặc nếu DB đã có cột GuestName/Phone thì gán trực tiếp
                    special_request = $"[Khách: {full_name} - {phone}] {special_request}",

                    // created_at = DateTime.Now // Uncomment nếu DB có cột này
                };

                db.Bookings.Add(booking);
                db.SaveChanges();

                // 5. Chuyển hướng sang trang Thành công / Thanh toán
                return RedirectToAction("Success", new { id = booking.booking_id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi hệ thống: " + ex.Message;
                return RedirectToAction("DatBan", new { id = restaurant_id });
            }
        }

        // ==================================================
        // 3. TRANG THÀNH CÔNG (GET)
        // ==================================================
        public ActionResult Success(int id)
        {
            var booking = db.Bookings
                            .Include(b => b.Restaurants)
                            .FirstOrDefault(b => b.booking_id == id);

            if (booking == null) return HttpNotFound();

            return View(booking);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}