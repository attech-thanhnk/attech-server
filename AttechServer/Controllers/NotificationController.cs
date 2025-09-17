using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos.Notification;
using AttechServer.Shared.ApplicationBase.Common;
using AttechServer.Shared.Attributes;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.Filters;
using AttechServer.Shared.Consts;
using Microsoft.AspNetCore.Authorization;

namespace AttechServer.Controllers
{
    [Route("api/notification")]
    [ApiController]
    [Authorize]
    public class NotificationController : ApiControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(
            INotificationService notificationService,
            ILogger<NotificationController> logger)
            : base(logger)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get all notifications with caching
        /// </summary>
        [HttpGet("find-all")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.ShortCache, "notification", varyByQueryString: true)]
        public async Task<ApiResponse> FindAll([FromQuery] PagingRequestBaseDto input)
        {
            try
            {
                var result = await _notificationService.FindAll(input);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all notifications");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get notifications by category slug with caching
        /// </summary>
        [HttpGet("category/{slug}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.ShortCache, "notification-category", varyByQueryString: true)]
        public async Task<ApiResponse> FindAllByCategorySlug([FromQuery] PagingRequestBaseDto input, string slug)
        {
            try
            {
                var result = await _notificationService.FindAllByCategorySlug(input, slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications by category slug");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get notification by ID with attachments included
        /// </summary>
        [HttpGet("detail/{id}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "admin-notification-detail")]
        public async Task<ApiResponse> FindById(int id)
        {
            try
            {
                var result = await _notificationService.FindById(id);
                // TODO: Update service to return NotificationWithAttachmentsDto that includes attachments by default
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification by id");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get notification by slug with caching
        /// </summary>
        [HttpGet("detail/slug/{slug}")]
        [AllowAnonymous]
        [CacheResponse(CacheProfiles.MediumCache, "notification-detail-slug")]
        public async Task<ApiResponse> FindBySlug(string slug)
        {
            try
            {
                var result = await _notificationService.FindBySlug(slug);
                return new ApiResponse(ApiStatusCode.Success, result, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification by slug");
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create new notification with all data in one request (FormData)
        /// </summary>
        [HttpPost("create")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Create([FromBody] CreateNotificationDto input)
        {
            try
            {
                _logger.LogInformation("=== DEBUG NOTIFICATION CREATE START ===");
                _logger.LogInformation("TitleVi: {Title}", input.TitleVi);
                _logger.LogInformation("ContentVi length: {Length}", input.ContentVi?.Length ?? 0);
                _logger.LogInformation("NotificationCategoryId: {CategoryId}", input.NotificationCategoryId);

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

                _logger.LogInformation("Calling NotificationService.Create...");
                var result = await _notificationService.Create(input);
                _logger.LogInformation("NotificationService.Create completed successfully");
                _logger.LogInformation("=== DEBUG NOTIFICATION CREATE END ===");

                return new ApiResponse(ApiStatusCode.Success, result, 200, "Tạo thông báo thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR CREATING NOTIFICATION ===");
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
        /// Update notification (handles text + files)
        /// ID được truyền qua route parameter, FE không cần gửi ID trong body
        /// </summary>
        [HttpPut("update/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Update(int id, [FromBody] UpdateNotificationDto input)
        {
            try
            {
                _logger.LogInformation("Updating notification with all data in one atomic operation");
                var result = await _notificationService.Update(id, input);
                return result != null
                    ? new ApiResponse(ApiStatusCode.Success, result, 200, "Cập nhật thông báo thành công")
                    : new ApiResponse(ApiStatusCode.Success, null, 200, "Cập nhật thông báo thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notification");
                return OkException(ex);
            }
        }


        /// <summary>
        /// Delete notification
        /// </summary>
        [HttpDelete("delete/{id}")]
        [RoleFilter(3)]
        public async Task<ApiResponse> Delete(int id)
        {
            try
            {
                await _notificationService.Delete(id);
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Xóa thông báo thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification");
                return OkException(ex);
            }
        }
    }
}
