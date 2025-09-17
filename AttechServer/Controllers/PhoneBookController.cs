using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.PhoneBook;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/phonebook")]
    [ApiController]
    public class PhoneBookController : ApiControllerBase
    {
        private readonly IPhoneBookService _phoneBookService;

        public PhoneBookController(IPhoneBookService phoneBookService, ILogger<PhoneBookController> logger)
            : base(logger)
        {
            _phoneBookService = phoneBookService;
        }

        /// <summary>
        /// Get all phone book entries with filtering and sorting (Admin only)
        /// </summary>
        [HttpGet("find-all")]
        [Authorize]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.ShortCache, "admin-phonebook", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _phoneBookService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all phone book entries");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get phone book entry detail by ID (Admin only)
        /// </summary>
        [HttpGet("find-by-id/{id}")]
        [Authorize]
        [RoleFilter(2)]
        [CacheResponse(CacheProfiles.MediumCache, "admin-phonebook-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _phoneBookService.FindById(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting phone book entry by id");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new phone book entry (Admin only)
        /// </summary>
        [HttpPost("create")]
        [Authorize]
        [RoleFilter(2)]
        public async Task<ApiResponse> Create([FromBody] CreatePhoneBookDto input)
        {
            try
            {
                var id = await _phoneBookService.Create(input);
                return new ApiResponse(ApiStatusCode.Success, new { id }, 200, "Tạo danh bạ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating phone book entry");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update phone book entry (Admin only)
        /// </summary>
        [HttpPut("update/{id}")]
        [Authorize]
        [RoleFilter(2)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdatePhoneBookDto input)
        {
            try
            {
                var result = await _phoneBookService.Update(id, input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật danh bạ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone book entry");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Delete phone book entry (Admin only)
        /// </summary>
        [HttpDelete("delete/{id}")]
        [Authorize]
        [RoleFilter(2)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _phoneBookService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa danh bạ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting phone book entry");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get phone book entries by department (Public API)
        /// </summary>
        [HttpGet("by-department/{department}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "public-phonebook-department")]
        public async Task<ApiResponse> GetByDepartment(string department)
        {
            try
            {
                var result = await _phoneBookService.GetByDepartment(department);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting phone book entries by department");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get all active phone book entries (Public API)
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.LongCache, "public-phonebook-active")]
        public async Task<ApiResponse> GetActiveContacts()
        {
            try
            {
                var result = await _phoneBookService.GetActiveContacts();
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active phone book entries");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Update phone book entry order (Admin only)
        /// </summary>
        [HttpPut("update-order/{id}")]
        [Authorize]
        [RoleFilter(2)]
        public async Task<ApiResponse> UpdateOrder(int id, [FromBody] int newOrder)
        {
            try
            {
                var result = await _phoneBookService.UpdateOrder(id, newOrder);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật thứ tự thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone book entry order");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Toggle active status of phone book entry (Admin only)
        /// </summary>
        [HttpPut("toggle-active/{id}")]
        [Authorize]
        [RoleFilter(2)]
        public async Task<ApiResponse> ToggleActive(int id)
        {
            try
            {
                var result = await _phoneBookService.ToggleActive(id);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật trạng thái thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling phone book entry active status");
                return OkException(ex);
            }
        }

    }
}