using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.News
{
    public class CreateAlbumDto
    {
        [Required(ErrorMessage = "Tiêu đề tiếng Việt là bắt buộc")]
        public string TitleVi { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Tiêu đề tiếng Anh là bắt buộc")]
        public string TitleEn { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Danh mục tin tức không hợp lệ")]
        public int NewsCategoryId { get; set; }

        // Featured image (required for album)
        [Range(1, int.MaxValue, ErrorMessage = "Ảnh đại diện là bắt buộc")]
        public int FeaturedImageId { get; set; }

        // Album gallery images (optional - có thể tạo album chỉ với ảnh đại diện)
        public List<int>? AttachmentIds { get; set; } = new();

        [Required(ErrorMessage = "Trạng thái là bắt buộc")]
        public int Status { get; set; }

        [Required(ErrorMessage = "Thời gian đăng là bắt buộc")]
        public DateTime TimePosted { get; set; }
    }
}