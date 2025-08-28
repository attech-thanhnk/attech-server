using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Shared.Consts;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttechServer.Controllers
{
    [Route("api/language-content-categories")]
    [ApiController]
    [Authorize]
    public class LanguageContentCategoryController : ApiControllerBase
    {
        private readonly ILanguageContentCategoryService _categoryService;

        public LanguageContentCategoryController(
            ILanguageContentCategoryService categoryService,
            ILogger<LanguageContentCategoryController> logger)
            : base(logger)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Get all language content categories
        /// </summary>
        [HttpGet("find-all")]
        public async Task<ApiResponse> FindAll()
        {
            try
            {
                var result = await _categoryService.GetAllCategories();
                var response = new { items = result };
                return new ApiResponse(ApiStatusCode.Success, response, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all language content categories");
                return OkException(ex);
            }
        }
    }
}