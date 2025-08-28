using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.LanguageContent
{
    public class CreateLanguageContentDto
    {
        [Required(ErrorMessage = "Content key is required")]
        [StringLength(255, ErrorMessage = "Content key cannot exceed 255 characters")]
        public string ContentKey { get; set; } = null!;

        public string? ValueVi { get; set; }

        public string? ValueEn { get; set; }

        [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
        public string Category { get; set; } = "common";

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
    }
}