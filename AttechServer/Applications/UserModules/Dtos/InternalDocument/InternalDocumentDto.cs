using AttechServer.Applications.UserModules.Dtos.Attachment;

namespace AttechServer.Applications.UserModules.Dtos.InternalDocument
{
    public class InternalDocumentDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? AttachmentId { get; set; }
        public int Status { get; set; }
        public DateTime TimePosted { get; set; }
        public AttachmentDto? Attachment { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}