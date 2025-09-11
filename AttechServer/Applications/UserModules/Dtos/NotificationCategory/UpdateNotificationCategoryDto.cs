using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.NotificationCategory
{
    public class UpdateNotificationCategoryDto
    {
        [Required(ErrorMessage = "ID là bắt buộc")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục thông báo là bắt buộc")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự")]
        public string TitleVi { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Tên danh mục tiếng Anh không được vượt quá 100 ký tự")]
        public string TitleEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug tiếng Việt là bắt buộc")]
        [StringLength(100, ErrorMessage = "Slug không được vượt quá 100 ký tự")]
        public string SlugVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Slug tiếng Anh là bắt buộc")]
        [StringLength(100, ErrorMessage = "Slug không được vượt quá 100 ký tự")]
        public string SlugEn { get; set; } = string.Empty;

        [StringLength(160, ErrorMessage = "Mô tả không được vượt quá 160 ký tự")]
        public string DescriptionVi { get; set; } = string.Empty;
        
        [StringLength(160, ErrorMessage = "Mô tả tiếng Anh không được vượt quá 160 ký tự")]
        public string DescriptionEn { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public int Order { get; set; } = 0;
        public int Status { get; set; }
    }
} 
