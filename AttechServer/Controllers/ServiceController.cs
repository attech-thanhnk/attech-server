using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Service;
using AttechServer.Applications.UserModules.Dtos.Attachment;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/service")]
    [ApiController]
    [Authorize]
    public class ServiceController : ApiControllerBase
    {
        private readonly IServiceService _serviceService;

        public ServiceController(IServiceService serviceService, ILogger<ServiceController> logger)
            : base(logger)
        {
            _serviceService = serviceService;
        }

        /// <summary>
        /// Get all services with caching
        /// </summary>
        [HttpGet("find-all")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.ShortCache, "services", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _serviceService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all services");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get service by ID with attachments included
        /// </summary>
        [HttpGet("detail/{id}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "admin-service-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _serviceService.FindById(id);
                // TODO: Update service to return ServiceWithAttachmentsDto that includes attachments by default
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service by id");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get service by slug with caching
        /// </summary>
        [HttpGet("detail/slug/{slug}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "service-detail-slug")]
        public async Task<ApiResponse> FindBySlug(string slug)
        {
            try
            {
                var result = await _serviceService.FindBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service by slug");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new service with all data in one request
        /// </summary>
        [HttpPost("create")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Create([FromBody] CreateServiceDto input)
        {
            try
            {
                _logger.LogInformation("=== DEBUG SERVICE CREATE START ===");
                _logger.LogInformation("TitleVi: {Title}", input.TitleVi);
                _logger.LogInformation("ContentVi length: {Length}", input.ContentVi?.Length ?? 0);
                
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

                _logger.LogInformation("Calling ServiceService.Create...");
                var result = await _serviceService.Create(input);
                _logger.LogInformation("ServiceService.Create completed successfully");
                _logger.LogInformation("=== DEBUG SERVICE CREATE END ===");
                
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo dịch vụ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR CREATING SERVICE ===");
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
        /// Update service (handles text + files)
        /// ID được truyền qua route parameter, FE không cần gửi ID trong body
        /// </summary>
        [HttpPut("update/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateServiceDto input)
        {
            try
            {
                _logger.LogInformation("Updating service with all data in one atomic operation");
                var result = await _serviceService.Update(id, input);
                return result != null 
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật dịch vụ thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật dịch vụ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Delete service
        /// </summary>
        [HttpDelete("delete/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _serviceService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa dịch vụ thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service");
                return OkException(ex);
            }
        }

    }
}
