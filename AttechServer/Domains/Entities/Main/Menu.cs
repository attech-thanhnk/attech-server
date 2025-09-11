using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(Menu))]
    [Index(
        nameof(Id),
        nameof(Deleted),
        Name = $"IX_{nameof(Menu)}",
        IsUnique = false
    )]
    [Index(nameof(ParentId))]
    [Index(nameof(Order))]
    public class Menu : IFullAudited
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string TitleVi { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string TitleEn { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [StringLength(500)]
        public string Url { get; set; } = string.Empty;

        [StringLength(500)]
        public string UrlEn { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        public int Order { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public bool IsExternal { get; set; } = false;

        [StringLength(50)]
        public string Target { get; set; } = "_self"; // _self, _blank

        [StringLength(500)]
        public string DescriptionVi { get; set; } = string.Empty;

        [StringLength(500)]
        public string DescriptionEn { get; set; } = string.Empty;

        // Menu classification
        [StringLength(50)]
        public string MenuType { get; set; } = "static"; // static, category_root, category_branch, category_leaf, item

        // Source tracking cho dynamic menus
        [StringLength(50)]
        public string? SourceType { get; set; } // NewsCategory, ProductCategory, News, Product

        public int? SourceId { get; set; } // ID của record gốc

        public int? SourceParentId { get; set; } // ParentId của record gốc (for mapping)

        // Navigation properties
        public Menu? Parent { get; set; }
        public List<Menu> Children { get; set; } = new();

        #region IFullAudited
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public bool Deleted { get; set; }
        #endregion
    }
}