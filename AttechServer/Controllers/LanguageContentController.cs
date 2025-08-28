using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.LanguageContent;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Consts;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Controllers
{
    [Route("api/language-contents")]
    [ApiController]
    [Authorize]
    public class LanguageContentController : ApiControllerBase
    {
        private readonly ILanguageContentService _languageContentService;

        public LanguageContentController(
            ILanguageContentService languageContentService,
            ILogger<LanguageContentController> logger)
            : base(logger)
        {
            _languageContentService = languageContentService;
        }

        /// <summary>
        /// Get all language contents with pagination and filtering
        /// </summary>
        [HttpGet("find-all")]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _languageContentService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all language contents");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get language content by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _languageContentService.FindById(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting language content by id: {Id}", id);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new language content
        /// </summary>
        [HttpPost("create")]
        public async Task<ApiResponse> Create([FromBody] CreateLanguageContentDto input)
        {
            try
            {
                var result = await _languageContentService.Create(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo nội dung ngôn ngữ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating language content");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update language content
        /// </summary>
        [HttpPut("update/{id}")]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateLanguageContentDto input)
        {
            try
            {
                var result = await _languageContentService.Update(id, input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật nội dung ngôn ngữ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating language content with id: {Id}", id);
                return OkException(ex);
            }
        }

        /// <summary>
        /// Delete language content
        /// </summary>
        [HttpDelete("delete/{id}")]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _languageContentService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa nội dung ngôn ngữ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting language content with id: {Id}", id);
                return OkException(ex);
            }
        }
    }
}