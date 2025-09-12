using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.InternalDocument
{
    public class UpdateInternalDocumentDto
    {
        [Required, StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public int? AttachmentId { get; set; }

        public int Status { get; set; } = 1;

        public DateTime? TimePosted { get; set; }
    }
}