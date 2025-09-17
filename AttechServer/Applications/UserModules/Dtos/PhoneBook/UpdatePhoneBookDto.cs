using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.PhoneBook
{
    public class UpdatePhoneBookDto
    {
        [StringLength(255, ErrorMessage = "Họ tên không được vượt quá 255 ký tự")]
        public string? FullName { get; set; }

        [StringLength(255, ErrorMessage = "Chức danh không được vượt quá 255 ký tự")]
        public string? Position { get; set; }

        [StringLength(255, ErrorMessage = "Tổ chức không được vượt quá 255 ký tự")]
        public string? Organization { get; set; }

        [StringLength(255, ErrorMessage = "Phòng ban không được vượt quá 255 ký tự")]
        public string? Department { get; set; }

        [StringLength(50, ErrorMessage = "Điện thoại không được vượt quá 50 ký tự")]
        public string? Phone { get; set; }

        [StringLength(20, ErrorMessage = "Máy lẻ không được vượt quá 20 ký tự")]
        public string? Extension { get; set; }

        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [StringLength(255, ErrorMessage = "Email không được vượt quá 255 ký tự")]
        public string? Email { get; set; }

        [StringLength(50, ErrorMessage = "Di động không được vượt quá 50 ký tự")]
        public string? Mobile { get; set; }

        [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự")]
        public string? Notes { get; set; }

        public int Order { get; set; }

        public bool IsActive { get; set; }
    }
}