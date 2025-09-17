using AttechServer.Domains.EntityBase;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttechServer.Domains.Entities.Main
{
    [Table(nameof(PhoneBook))]
    [Index(
        nameof(Id),
        nameof(Deleted),
        Name = $"IX_{nameof(PhoneBook)}",
        IsUnique = false
    )]
    [Index(nameof(Email))]
    [Index(nameof(Organization))]
    [Index(nameof(Department))]
    [Index(nameof(Position))]
    public class PhoneBook : IFullAudited
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Họ tên
        /// </summary>
        [StringLength(255)]
        public string? FullName { get; set; }

        /// <summary>
        /// Chức danh
        /// </summary>
        [StringLength(255)]
        public string? Position { get; set; }

        /// <summary>
        /// Tổ chức
        /// </summary>
        [StringLength(255)]
        public string? Organization { get; set; }

        /// <summary>
        /// Phòng ban
        /// </summary>
        [StringLength(255)]
        public string? Department { get; set; }

        /// <summary>
        /// Điện thoại cố định
        /// </summary>
        [StringLength(50)]
        public string? Phone { get; set; }

        /// <summary>
        /// Máy lẻ
        /// </summary>
        [StringLength(20)]
        public string? Extension { get; set; }

        /// <summary>
        /// Thư điện tử
        /// </summary>
        [StringLength(255)]
        public string? Email { get; set; }

        /// <summary>
        /// Di động
        /// </summary>
        [StringLength(50)]
        public string? Mobile { get; set; }

        /// <summary>
        /// Ghi chú bổ sung
        /// </summary>
        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Thứ tự hiển thị
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Trạng thái hiển thị: true = hiển thị, false = ẩn
        /// </summary>
        public bool IsActive { get; set; } = true;

        // IFullAudited properties
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ModifiedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public bool Deleted { get; set; } = false;
    }
}