using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.News
{
    public class CreateDocumentDto
    {
        [Required(ErrorMessage = "Tiêu đề tiếng Việt là bắt buộc")]
        public string TitleVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Tiêu đề tiếng Anh là bắt buộc")]
        public string TitleEn { get; set; } = string.Empty;

        public string? DescriptionVi { get; set; }
        public string? DescriptionEn { get; set; }

        [Required(ErrorMessage = "Trạng thái là bắt buộc")]
        public int Status { get; set; }

        [Required(ErrorMessage = "Thời gian đăng là bắt buộc")]
        public DateTime TimePosted { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Danh mục tin tức không hợp lệ")]
        public int NewsCategoryId { get; set; }

        // Featured image (optional for document)
        public int? FeaturedImageId { get; set; }

        // Document files (required for document)
        [Required(ErrorMessage = "Ít nhất một tài liệu là bắt buộc")]
        public List<int> AttachmentIds { get; set; } = new();
    }
}