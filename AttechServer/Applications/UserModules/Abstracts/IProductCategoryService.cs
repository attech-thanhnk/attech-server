using AttechServer.Applications.UserModules.Dtos.ProductCategory;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Applications.UserModules.Abstracts
{
    public interface IProductCategoryService
    {
        Task<PagingResult<ProductCategoryDto>> FindAll(PagingRequestBaseDto input);

        Task<DetailProductCategoryDto> FindById(int id);

        Task<DetailProductCategoryDto> FindBySlug(string slug);

        Task<ProductCategoryDto> Create(CreateProductCategoryDto input);

        Task<ProductCategoryDto> Update(UpdateProductCategoryDto input);

        Task Delete(int id);

        Task UpdateStatusProductCategory(int id, int status);

        Task<bool> HasChildrenAsync(int categoryId);

        /// <summary>
        /// Lấy tất cả ID danh mục con và cháu
        /// </summary>
        /// <param name="parentId"></param>
        /// <returns></returns>
        Task<List<int>> GetDescendantIdsAsync(int parentId);

        /// <summary>
        /// Lấy breadcrumb từ danh mục hiện tại lên root
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        Task<List<ProductCategoryDto>> GetBreadcrumbAsync(int categoryId);
    }
}
