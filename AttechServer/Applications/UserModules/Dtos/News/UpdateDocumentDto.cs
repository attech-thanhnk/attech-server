using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.News
{
    public class UpdateDocumentDto
    {
        [Required(ErrorMessage = "Tiêu đề tiếng Việt là bắt buộc")]
        public string TitleVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Tiêu đề tiếng Anh là bắt buộc")]
        public string TitleEn { get; set; } = string.Empty;

        public string? DescriptionVi { get; set; }
        public string? DescriptionEn { get; set; }

        [Range(0, 1, ErrorMessage = "Trạng thái phải là 0 (không hoạt động) hoặc 1 (hoạt động)")]
        public int Status { get; set; } = 1;

        [Required(ErrorMessage = "Thời gian đăng là bắt buộc")]
        public DateTime TimePosted { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Danh mục tin tức không hợp lệ")]
        public int NewsCategoryId { get; set; }

        // Featured image (optional for document)
        public int? FeaturedImageId { get; set; }

        // Document files (optional - null means no change, empty list means clear all)
        public List<int>? AttachmentIds { get; set; }
    }
}