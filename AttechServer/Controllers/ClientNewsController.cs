using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.News;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/news/client")]
    [ApiController]
    [AllowAnonymous]
    public class ClientNewsController : ApiControllerBase
    {
        private readonly INewsService _newsService;

        public ClientNewsController(
            INewsService newsService,
            ILogger<ClientNewsController> logger) 
            : base(logger)
        {
            _newsService = newsService;
        }

        /// <summary>
        /// Get all published news (status = 1) with caching for client
        /// </summary>
        [HttpGet("find-all")]
        [CacheResponse(CacheProfiles.ShortCache, "client-news", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.FindAllForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all published news for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get published news detail by slug (status = 1 only) for client
        /// </summary>
        [HttpGet("detail/{slug}")]
        [CacheResponse(CacheProfiles.MediumCache, "client-news-detail")]
        public async Task<ApiResponse> FindBySlug(string slug)
        {
            try
            {
                var result = await _newsService.FindBySlugForClient(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published news by slug for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get published news by category slug (status = 1 only) for client
        /// </summary>
        [HttpGet("category/{slug}")]
        [CacheResponse(CacheProfiles.ShortCache, "client-news-category", varyByQueryString: true)]
        public async Task<ApiResponse> FindAllByCategorySlug([FromQuery] PagingRequestBaseDto input, string slug)
        {
            try
            {
                var result = await _newsService.FindAllByCategorySlugForClient(input, slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting published news by category slug for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Search published news by keyword (status = 1 only) for client
        /// </summary>
        [HttpGet("search")]
        [CacheResponse(CacheProfiles.ShortCache, "client-news-search", varyByQueryString: true)]
        public async Task<ApiResponse> Search([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input.Keyword))
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 400, "Keyword is required for search");
                }

                var result = await _newsService.SearchForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching published news for client with keyword: {Keyword}", input.Keyword);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get outstanding published news (isOutstanding = true, status = 1) for client
        /// </summary>
        [HttpGet("outstanding")]
        [CacheResponse(CacheProfiles.ShortCache, "client-news-outstanding", varyByQueryString: true)]
        public async Task<ApiResponse> FindOutstanding([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _newsService.FindOutstandingForClient(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting outstanding published news for client");
                return OkException(ex);
            }
        }
    }
}