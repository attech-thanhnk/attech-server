using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(NotificationCategory))]
    [Index(
        nameof(Id),
        nameof(Deleted),
        Name = $"IX_{nameof(NotificationCategory)}",
        IsUnique = false
    )]
    [Index(nameof(SlugVi), IsUnique = true)]
    [Index(nameof(SlugEn), IsUnique = true)]
    public class NotificationCategory : IFullAudited
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string TitleVi { get; set; } = string.Empty;
        [Required, StringLength(100)]
        public string TitleEn { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string SlugVi { get; set; } = string.Empty;
        [Required, StringLength(100)]
        public string SlugEn { get; set; } = string.Empty;

        [StringLength(160)]
        public string DescriptionVi { get; set; } = string.Empty;
        [StringLength(160)]
        public string DescriptionEn { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public int Order { get; set; } = 0;
        public int Status { get; set; }

        // Navigation properties
        public NotificationCategory? Parent { get; set; }
        public List<NotificationCategory> Children { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        #region audit
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public bool Deleted { get; set; }
        #endregion
    }
} 
