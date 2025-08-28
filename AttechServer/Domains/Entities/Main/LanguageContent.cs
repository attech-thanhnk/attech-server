using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(LanguageContent))]
    [Index(
        nameof(Id),
        nameof(Deleted),
        Name = $"IX_{nameof(LanguageContent)}",
        IsUnique = false
    )]
    [Index(nameof(ContentKey), IsUnique = true)]
    [Index(nameof(Category))]
    public class LanguageContent : IFullAudited
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(255)]
        public string ContentKey { get; set; } = null!;

        public string? ValueVi { get; set; }

        public string? ValueEn { get; set; }

        [StringLength(100)]
        public string Category { get; set; } = "common";

        [StringLength(500)]
        public string? Description { get; set; }

        #region audit
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public bool Deleted { get; set; }
        #endregion
    }
}