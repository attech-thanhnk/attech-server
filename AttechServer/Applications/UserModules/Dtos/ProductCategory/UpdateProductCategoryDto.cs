
using System.ComponentModel.DataAnnotations;

namespace AttechServer.Applications.UserModules.Dtos.ProductCategory
{
    public class UpdateProductCategoryDto : CreateProductCategoryDto
    {
        [Required(ErrorMessage = "ID là bắt buộc")]
        public int Id { get; set; }
    }
}
