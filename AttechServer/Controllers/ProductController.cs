using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Product;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/product")]
    [ApiController]
    [Authorize]
    public class ProductController : ApiControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(
            IProductService productService,
            ILogger<ProductController> logger)
            : base(logger)
        {
            _productService = productService;
        }

        /// <summary>
        /// Get all products with caching
        /// </summary>
        [HttpGet("find-all")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.ShortCache, "product", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _productService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all products");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get products by category slug with caching
        /// </summary>
        [HttpGet("category/{slug}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.ShortCache, "product-category", varyByQueryString: true)]
        public async Task<ApiResponse> FindAllByCategorySlug([FromQuery] PagingRequestBaseDto input, string slug)
        {
            try
            {
                var result = await _productService.FindAllByCategorySlug(input, slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category slug");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get product by ID with attachments included
        /// </summary>
        [HttpGet("detail/{id}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "admin-product-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _productService.FindById(id);
                // TODO: Update service to return ProductWithAttachmentsDto that includes attachments by default
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by id");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get product by slug with caching
        /// </summary>
        [HttpGet("detail/slug/{slug}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "product-detail-slug")]
        public async Task<ApiResponse> FindBySlug(string slug)
        {
            try
            {
                var result = await _productService.FindBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product by slug");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new product with all data in one request (FormData)
        /// </summary>
        [HttpPost("create")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Create([FromBody] CreateProductDto input)
        {
            try
            {
                _logger.LogInformation("=== DEBUG PRODUCT CREATE START ===");
                _logger.LogInformation("TitleVi: {Title}", input.TitleVi);
                _logger.LogInformation("ContentVi length: {Length}", input.ContentVi?.Length ?? 0);
                _logger.LogInformation("ProductCategoryId: {CategoryId}", input.ProductCategoryId);

                // Log attachment IDs
                if (input.FeaturedImageId.HasValue)
                {
                    _logger.LogInformation("FeaturedImageId: {FeaturedImageId}", input.FeaturedImageId.Value);
                }
                else
                {
                    _logger.LogInformation("FeaturedImageId: NULL");
                }

                if (input.AttachmentIds != null && input.AttachmentIds.Any())
                {
                    _logger.LogInformation("AttachmentIds: {AttachmentIds}", string.Join(",", input.AttachmentIds));
                }

                _logger.LogInformation("Calling ProductService.Create...");
                var result = await _productService.Create(input);
                _logger.LogInformation("ProductService.Create completed successfully");
                _logger.LogInformation("=== DEBUG PRODUCT CREATE END ===");

                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo sản phẩm thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR CREATING PRODUCT ===");
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Exception Message: {Message}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);

                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerMessage}", ex.InnerException.Message);
                }

                return OkException(ex);
            }
        }


        /// <summary>
        /// Update product (handles text + files)
        /// ID được truyền qua route parameter, FE không cần gửi ID trong body
        /// </summary>
        [HttpPut("update/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateProductDto input)
        {
            try
            {
                _logger.LogInformation("Updating product with all data in one atomic operation");
                var result = await _productService.Update(id, input);
                return result != null
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật sản phẩm thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật sản phẩm thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                return OkException(ex);
            }
        }


        /// <summary>
        /// Delete product
        /// </summary>
        [HttpDelete("delete/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _productService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa sản phẩm thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product");
                return OkException(ex);
            }
        }
    }
}
