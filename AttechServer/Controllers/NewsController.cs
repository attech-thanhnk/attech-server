using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.News;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/news")]
    [ApiController]
    [Authorize]
    public class NewsController : ApiControllerBase
    {
        private readonly INewsService _newsService;

        public NewsController(
            INewsService newsService,
            ILogger<NewsController> logger)
            : base(logger)
        {
            _newsService = newsService;
        }

        /// <summary>
        /// Get all news (all status) for admin
        /// </summary>
        [HttpGet("find-all")]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.ShortCache, "admin-news", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all news for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get news by category slug (all status) for admin
        /// </summary>
        [HttpGet("category/{slug}")]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.ShortCache, "admin-news-category", varyByQueryString: true)]
        public async Task<ApiResponse> FindAllByCategorySlug([FromQuery] PagingRequestBaseDto input, string slug)
        {
            try
            {
                var result = await _newsService.FindAllByCategorySlug(input, slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news by category slug for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get news by ID (all status) for admin
        /// </summary>
        [HttpGet("detail/{id}")]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.MediumCache, "admin-news-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _newsService.FindById(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news by id for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get news by slug (all status) for admin
        /// </summary>
        [HttpGet("detail/slug/{slug}")]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.MediumCache, "admin-news-detail-slug")]
        public async Task<ApiResponse> FindBySlug(string slug)
        {
            try
            {
                var result = await _newsService.FindBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news by slug for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new news with all data in one request (FormData)
        /// </summary>
        [HttpPost("create")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Create([FromBody] CreateNewsDto input)
        {
            try
            {
                _logger.LogInformation("=== DEBUG NEWS CREATE START ===");
                _logger.LogInformation("TitleVi: {Title}", input.TitleVi);
                _logger.LogInformation("ContentVi length: {Length}", input.ContentVi?.Length ?? 0);
                _logger.LogInformation("NewsCategoryId: {CategoryId}", input.NewsCategoryId);

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

                _logger.LogInformation("Calling NewsService.Create...");
                var result = await _newsService.Create(input);
                _logger.LogInformation("NewsService.Create completed successfully");
                _logger.LogInformation("=== DEBUG NEWS CREATE END ===");

                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo tin tức thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR CREATING NEWS ===");
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
        /// Update news (handles text + files)
        /// ID được truyền qua route parameter, FE không cần gửi ID trong body
        /// </summary>
        [HttpPut("update/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateNewsDto input)
        {
            try
            {
                _logger.LogInformation("Updating news with all data in one atomic operation");
                var result = await _newsService.Update(id, input);
                return result != null
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật tin tức thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật tin tức thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating news");
                return OkException(ex);
            }
        }


        /// <summary>
        /// Delete news
        /// </summary>
        [HttpDelete("delete/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _newsService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa tin tức thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting news");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create album (news with IsAlbum = true)
        /// </summary>
        [HttpPost("create-album")]
        [RoleFilter(2)]
        public async Task<ApiResponse> CreateAlbum([FromBody] CreateAlbumDto input)
        {
            try
            {
                _logger.LogInformation("Creating album: {Title}", input.TitleVi);
                var result = await _newsService.CreateAlbum(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo album thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating album");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get all albums for admin management (all status)
        /// </summary>
        [HttpGet("albums")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetAlbums([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.GetAlbums(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting albums for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get album detail by ID for admin
        /// </summary>
        [HttpGet("albums/{id:int}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetAlbumById(int id)
        {
            try
            {
                var result = await _newsService.GetAlbumByIdForAdmin(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting album by ID: {Id}", id);
                return OkException(ex);
            }
        }


        /// <summary>
        /// Get news gallery by slug
        /// </summary>
        [HttpGet("gallery/{slug}")]
        [RoleFilter(2)]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> GetGalleryBySlug(string slug)
        {
            try
            {
                var result = await _newsService.GetGalleryBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting gallery by slug: {Slug}", slug);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update album (simplified version for albums only)
        /// </summary>
        [HttpPut("update-album/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> UpdateAlbum(int id, [FromBody] UpdateAlbumDto input)
        {
            try
            {
                _logger.LogInformation("Updating album: {Title}", input.TitleVi);
                var result = await _newsService.UpdateAlbum(id, input);
                return result != null
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật album thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật album thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating album");
                return OkException(ex);
            }
        }

        #region Document Endpoints

        /// <summary>
        /// Create document (news với IsDocument = true)
        /// </summary>
        [HttpPost("create-document")]
        [RoleFilter(2)]
        public async Task<ApiResponse> CreateDocument([FromBody] CreateDocumentDto input)
        {
            try
            {
                _logger.LogInformation("Creating document: {Title}", input.TitleVi);
                var result = await _newsService.CreateDocument(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo tài liệu thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get all documents for admin management (all status)
        /// </summary>
        [HttpGet("documents")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetDocuments([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.GetDocuments(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents for admin");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get document detail by ID for admin
        /// </summary>
        [HttpGet("documents/{id:int}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> GetDocumentById(int id)
        {
            try
            {
                var result = await _newsService.GetDocumentByIdForAdmin(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document by ID: {Id}", id);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update document (simplified version for documents only)
        /// </summary>
        [HttpPut("update-document/{id}")]
        [RoleFilter(2)]
        public async Task<ApiResponse> UpdateDocument(int id, [FromBody] UpdateDocumentDto input)
        {
            try
            {
                _logger.LogInformation("Updating document: {Title}", input.TitleVi);
                var result = await _newsService.UpdateDocument(id, input);
                return result != null
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật tài liệu thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật tài liệu thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get document gallery by slug
        /// </summary>
        [HttpGet("gallery-document/{slug}")]
        [RoleFilter(2)]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> GetGalleryDocumentBySlug(string slug)
        {
            try
            {
                var result = await _newsService.GetGalleryDocumentBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document gallery by slug: {Slug}", slug);
                return OkException(ex);
            }
        }

        #endregion
    }
}
