using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(LanguageContentCategory))]
    [Index(
        nameof(Id),
        Name = $"IX_{nameof(LanguageContentCategory)}",
        IsUnique = false
    )]
    [Index(nameof(Name), IsUnique = true)]
    public class LanguageContentCategory : ICreatedBy
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = null!;

        [Required, StringLength(200)]
        public string DisplayName { get; set; } = null!;

        #region audit
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        #endregion
    }
}