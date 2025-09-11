using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.News;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Controllers
{
    [Route("api/client/documents")]
    [ApiController]
    public class ClientDocumentController : ApiControllerBase
    {
        private readonly INewsService _newsService;

        public ClientDocumentController(
            INewsService newsService,
            ILogger<ClientDocumentController> logger)
            : base(logger)
        {
            _newsService = newsService;
        }

        /// <summary>
        /// Get all published documents (status = 1) for client
        /// </summary>
        [HttpGet]
        [CacheResponse(300)] // 5 minutes cache
        public async Task<ApiResponse> GetPublishedDocuments([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.GetPublishedDocumentsForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published documents for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get document detail by slug (published only)
        /// </summary>
        [HttpGet("{slug}")]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> GetDocumentBySlug(string slug)
        {
            try
            {
                var result = await _newsService.GetPublishedDocumentBySlugForClient(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published document by slug: {Slug}", slug);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get documents by category (published only)
        /// </summary>
        [HttpGet("category/{categorySlug}")]
        [CacheResponse(300)] // 5 minutes cache
        public async Task<ApiResponse> GetDocumentsByCategory(string categorySlug, [FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.GetPublishedDocumentsByCategoryForClient(categorySlug, input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents by category: {CategorySlug}", categorySlug);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get featured/outstanding documents (published only)
        /// </summary>
        [HttpGet("featured")]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> GetFeaturedDocuments([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.GetFeaturedDocumentsForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting featured documents for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get document files by slug
        /// </summary>
        [HttpGet("{slug}/files")]
        [CacheResponse(600)] // 10 minutes cache
        public async Task<ApiResponse> GetDocumentFiles(string slug)
        {
            try
            {
                var result = await _newsService.GetDocumentAttachmentsForClient(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document files: {Slug}", slug);
                return OkException(ex);
            }
        }
    }
}