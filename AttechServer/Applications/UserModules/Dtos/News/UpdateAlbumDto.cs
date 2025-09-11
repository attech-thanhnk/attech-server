using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.News
{
    public class UpdateAlbumDto
    {
        [Required(ErrorMessage = "Tiêu đề album tiếng Việt là bắt buộc")]
        [StringLength(200, ErrorMessage = "Tiêu đề tiếng Việt không được vượt quá 200 ký tự")]
        public string TitleVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Tiêu đề album tiếng Anh là bắt buộc")]
        [StringLength(200, ErrorMessage = "Tiêu đề tiếng Anh không được vượt quá 200 ký tự")]
        public string TitleEn { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
        public string? DescriptionVi { get; set; }
        
        [StringLength(500, ErrorMessage = "Mô tả tiếng Anh không được vượt quá 500 ký tự")]
        public string? DescriptionEn { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Danh mục tin tức không hợp lệ")]
        public int NewsCategoryId { get; set; }

        [Range(0, 1, ErrorMessage = "Trạng thái phải là 0 (không hoạt động) hoặc 1 (hoạt động)")]
        public int Status { get; set; } = 1;

        [Required(ErrorMessage = "Slug tiếng Việt là bắt buộc")]
        [StringLength(200, ErrorMessage = "Slug tiếng Việt không được vượt quá 200 ký tự")]
        public string SlugVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Slug tiếng Anh là bắt buộc")]
        [StringLength(200, ErrorMessage = "Slug tiếng Anh không được vượt quá 200 ký tự")]
        public string SlugEn { get; set; } = string.Empty;

        // Album images
        [Range(1, int.MaxValue, ErrorMessage = "Ảnh đại diện là bắt buộc")]
        public int FeaturedImageId { get; set; }
        
        // Album gallery images (optional)
        public List<int>? AttachmentIds { get; set; }

        [Required(ErrorMessage = "Thời gian đăng là bắt buộc")]
        public DateTime TimePosted { get; set; }
    }
}