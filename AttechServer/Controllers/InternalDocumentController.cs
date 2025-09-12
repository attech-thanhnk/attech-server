using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.InternalDocument;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using AttechServer.Shared.Consts;
using AttechServer.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Controllers
{
    [Route("api/internal-documents")]
    [ApiController]
    [Authorize]
    public class InternalDocumentController : ApiControllerBase
    {
        private readonly IInternalDocumentService _internalDocumentService;
        private readonly IWebHostEnvironment _environment;

        public InternalDocumentController(
            IInternalDocumentService internalDocumentService,
            IWebHostEnvironment environment,
            ILogger<InternalDocumentController> logger)
            : base(logger)
        {
            _internalDocumentService = internalDocumentService;
            _environment = environment;
        }

        #region Admin Operations (require high role)

        /// <summary>
        /// Get all internal documents (including drafts) for admin
        /// </summary>
        [HttpGet("find-all")]
        [RoleFilter(2)] // Admin role required
        public async Task<ApiResponse> FindAll([FromQuery] InternalDocumentPagingRequestDto input)
        {
            try
            {
                var result = await _internalDocumentService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all internal documents");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get internal document details by ID for admin (including drafts)
        /// </summary>
        [HttpGet("find-by-id/{id}")]
        [RoleFilter(2)] // Admin role required
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _internalDocumentService.FindById(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting internal document by ID: {Id}", id);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new internal document
        /// </summary>
        [HttpPost("create")]
        [RoleFilter(2)] // Admin role required
        public async Task<ApiResponse> Create([FromBody] CreateInternalDocumentDto input)
        {
            try
            {
                var result = await _internalDocumentService.Create(input);
                return new ApiResponse(ApiStatusCode.Success, result, 201, "Created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating internal document");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update internal document by ID
        /// </summary>
        [HttpPut("update/{id}")]
        [RoleFilter(2)] // Admin role required
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateInternalDocumentDto input)
        {
            try
            {
                var result = await _internalDocumentService.Update(id, input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating internal document with ID: {Id}", id);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Delete internal document by ID
        /// </summary>
        [HttpDelete("delete/{id}")]
        [RoleFilter(2)] // Admin role required
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _internalDocumentService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting internal document with ID: {Id}", id);
                return OkException(ex);
            }
        }

        #endregion

        #region User Operations (require authentication only)

        /// <summary>
        /// Get all published internal documents for authenticated users
        /// </summary>
        [HttpGet("find-all-published")]
        [CacheResponse(300)] // 5 minutes cache
        public async Task<ApiResponse> FindAllPublished([FromQuery] InternalDocumentPagingRequestDto input)
        {
            try
            {
                var result = await _internalDocumentService.GetPublishedDocumentsForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published internal documents");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get published internal documents by category for authenticated users
        /// </summary>
        [HttpGet("find-by-category/{category}")]
        [CacheResponse(300)] // 5 minutes cache
        public async Task<ApiResponse> FindByCategory(string category, [FromQuery] InternalDocumentPagingRequestDto input)
        {
            try
            {
                var result = await _internalDocumentService.GetPublishedDocumentsByCategoryForClient(category, input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published internal documents by category: {Category}", category);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get published internal document details by ID for authenticated users
        /// </summary>
        [HttpGet("find-published-by-id/{id}")]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> FindPublishedById(int id)
        {
            try
            {
                var result = await _internalDocumentService.GetPublishedDocumentByIdForClient(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published internal document by ID: {Id}", id);
                return OkException(ex);
            }
        }


        /// <summary>
        /// Get all categories for published documents
        /// </summary>
        [HttpGet("categories")]
        [CacheResponse(1800)] // 30 minutes cache
        public async Task<ApiResponse> GetCategories()
        {
            try
            {
                var result = await _internalDocumentService.GetCategories();
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting internal document categories");
                return OkException(ex);
            }
        }

        #endregion
    }
}