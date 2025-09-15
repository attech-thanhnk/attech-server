using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.LanguageContent;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/language-contents/client")]
    [ApiController]
    [AllowAnonymous]
    public class ClientLanguageContentController : ApiControllerBase
    {
        private readonly ILanguageContentService _languageContentService;

        public ClientLanguageContentController(
            ILanguageContentService languageContentService,
            ILogger<ClientLanguageContentController> logger)
            : base(logger)
        {
            _languageContentService = languageContentService;
        }

        /// <summary>
        /// Get all language contents with long cache for client
        /// </summary>
        [HttpGet("find-all")]
        [CacheResponse(CacheProfiles.LongCache, "client-language-contents")]
        public async Task<ApiResponse> FindAll()
        {
            try
            {
                var input = new PagingRequestBaseDto { PageSize = int.MaxValue, PageNumber = 1 };
                var result = await _languageContentService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all language contents for client");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get language content by ID with long cache for client
        /// </summary>
        [HttpGet("{id}")]
        [CacheResponse(CacheProfiles.LongCache, "client-language-content-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _languageContentService.FindById(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting language content by id: {Id} for client", id);
                return OkException(ex);
            }
        }
    }
}