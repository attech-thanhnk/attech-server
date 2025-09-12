using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(InternalDocument))]
    [Index(
        nameof(Id),
        nameof(Deleted),
        Name = $"IX_{nameof(InternalDocument)}",
        IsUnique = false
    )]
    [Index(nameof(Category))]
    [Index(nameof(Status))]
    public class InternalDocument : IFullAudited
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public int? AttachmentId { get; set; }

        /// <summary>
        /// Trạng thái
        /// 1 = Active, 0 = Inactive
        /// </summary>
        public int Status { get; set; } = 1;

        public DateTime TimePosted { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(AttachmentId))]
        public Attachment? Attachment { get; set; }

        #region IFullAudited
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public bool Deleted { get; set; }
        #endregion
    }
}