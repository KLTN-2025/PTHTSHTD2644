using System.ComponentModel.DataAnnotations;

namespace SmartTable.Models.ViewModels
{
    public class AdminChangePasswordViewModel
    {
        public int UserId { get; set; }

        public string Email { get; set; }

        public string FullName { get; set; }

        public string Role { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không trùng khớp.")]
        public string ConfirmPassword { get; set; }
    }
}
