using AttechServer.Applications.UserModules.Dtos.Product;
using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.ProductCategory
{
    public class DetailProductCategoryDto
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string TitleVi { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;

        [StringLength(100)]
        public string SlugVi { get; set; } = string.Empty;
        public string SlugEn { get; set; } = string.Empty;

        [StringLength(160)]
        public string DescriptionVi { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public int Order { get; set; }
        public int Status { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }

        public List<ProductDto> Products { get; set; } = new List<ProductDto>();
        public List<DetailProductCategoryDto> Children { get; set; } = new List<DetailProductCategoryDto>();
    }
}
