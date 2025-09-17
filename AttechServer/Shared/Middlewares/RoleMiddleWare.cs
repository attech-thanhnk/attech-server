using AttechServer.Shared.Consts.Exceptions;
using AttechServer.Shared.Consts;
using System.Text.Json;

namespace AttechServer.Shared.Middlewares
{
    public class RoleMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RoleMiddleWare> _logger;

        public RoleMiddleWare(
            RequestDelegate next,
            ILogger<RoleMiddleWare> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                // Check if the endpoint has [AllowAnonymous] attribute
                var endpoint = context.GetEndpoint();
                if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() != null)
                {
                    await _next(context);
                    return;
                }

                if (!context.User.Identity!.IsAuthenticated)
                {
                    await _next(context);
                    return;
                }

                var userId = GetUserId(context);
                var userLevel = GetUserLevel(context);
                
                _logger.LogInformation($"RoleMiddleware: userId={userId}, userLevel={userLevel}, path={context.Request.Path}");

                // Check if the endpoint has RoleFilter attribute
                var roleFilterAttribute = endpoint?.Metadata?.GetMetadata<AttechServer.Shared.Filters.RoleFilterAttribute>();

                if (roleFilterAttribute != null)
                {
                    // If endpoint has RoleFilter, use its logic
                    _logger.LogInformation($"Endpoint has RoleFilter, letting RoleFilter handle authorization");
                    await _next(context);
                    return;
                }

                // If no RoleFilter attribute, allow all authenticated users
                _logger.LogInformation($"No RoleFilter found, allowing authenticated user with userLevel={userLevel}");
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking role");
                await ReturnUnauthorizedResponse(context);
            }
        }

        private int GetUserId(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst("user_id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Invalid user ID");
            }
            return userId;
        }

        private int GetUserLevel(HttpContext context)
        {
            var userLevelClaim = context.User.FindFirst("user_level")?.Value;
            if (string.IsNullOrEmpty(userLevelClaim) || !int.TryParse(userLevelClaim, out int userLevel))
            {
                throw new UnauthorizedAccessException("Invalid user level");
            }
            return userLevel;
        }

        private async Task ReturnUnauthorizedResponse(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = ErrorMessage.UserNotHavePermission
            }, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
    }
}