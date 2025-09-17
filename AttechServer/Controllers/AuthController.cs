using AttechServer.Applications.UserModules.Abstracts;
using AttechServer.Applications.UserModules.Dtos;
using AttechServer.Applications.UserModules.Dtos.User;
using AttechServer.Shared.WebAPIBase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AttechServer.Shared.Consts;

namespace AttechServer.Controllers
{
    /// <summary>
    /// Authentication & Authorization Controller
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ApiControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IActivityLogService _activityLogService;
        
        public AuthController(ILogger<AuthController> logger, IAuthService authService, IActivityLogService activityLogService) : base(logger)
        {
            _authService = authService;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Login
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<ApiResponse> Login([FromBody] UserLoginDto input)
        {
            try
            {
                var result = await _authService.Login(input);
                
                // Log successful login
                await _activityLogService.LogUserActionAsync(
                    "login", 
                    $"User {input.Username} logged in successfully", 
                    details: $"IP: {HttpContext.Connection.RemoteIpAddress}"
                );
                
                return new(result);
            }
            catch (Exception ex)
            {
                // Log failed login attempt
                await _activityLogService.LogSecurityEventAsync(
                    "login_failed",
                    $"Failed login attempt for username: {input.Username}",
                    ex.Message
                );
                
                return OkException(ex);
            }
        }

        /// <summary>
        /// Create user (only for admin)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("register")]
        [Authorize] 
        public async Task<ApiResponse> Register([FromBody] CreateUserDto input)
        {
            try
            {
                _authService.RegisterUser(input);
                
                // Log successful registration
                var userTypeString = input.RoleId switch
                {
                    1 => "SuperAdmin",
                    2 => "Admin",
                    3 => "Editor",
                    4 => "User",
                    _ => "Unknown"
                };
                
                await _activityLogService.LogUserActionAsync(
                    "user_created",
                    $"New {userTypeString} user created: {input.Username}",
                    details: $"Email: {input.Email}, FullName: {input.FullName}, UserType: {userTypeString}"
                );
                
                return new();
            }
            catch (Exception ex)
            {
                // Log failed user creation
                await _activityLogService.LogSystemActionAsync(
                    "user_creation_failed",
                    $"Failed to create user: {input.Username}",
                    ex.Message
                );
                
                return OkException(ex);
            }
        }

        /// <summary>
        /// Refresh token
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("refresh-token")]
        public async Task<ApiResponse> RefreshToken([FromBody] TokenApiDto input)
        {
            try
            {
                return new(await _authService.RefreshToken(input));
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Get current user info 
        /// </summary>
        /// <returns></returns>
        [HttpGet("me")]
        [Authorize]
        public async Task<ApiResponse> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst("user_id")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 401, "Invalid token");
                }

                var user = await _authService.GetUserWithPermissionsAsync(userId);
                if (user is null)
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 404, "User not found");
                }

                return new ApiResponse(ApiStatusCode.Success, (object)user!, 200, "OK");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Log out
        /// </summary>
        /// <returns></returns>
        [HttpPost("logout")]
        [Authorize]
        public ApiResponse Logout()
        {
            try
            {
                return new ApiResponse(ApiStatusCode.Success, null, 200, "Logged out successfully");
            }
            catch (Exception ex)
            {
                return OkException(ex);
            }
        }

        /// <summary>
        /// Change password
        /// </summary>
        /// <param name="changePasswordDto"></param>
        /// <returns></returns>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ApiResponse> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                var userIdClaim = User.FindFirst("user_id")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return new ApiResponse(ApiStatusCode.Error, null, 401, "Invalid token");
                }

                await _authService.ChangePasswordAsync(userId, changePasswordDto);

                await _activityLogService.LogUserActionAsync(
                    "password_changed",
                    $"User {userId} changed password successfully",
                    details: $"IP: {HttpContext.Connection.RemoteIpAddress}"
                );

                return new ApiResponse(ApiStatusCode.Success, null, 200, "Đổi mật khẩu thành công");
            }
            catch (Exception ex)
            {
                await _activityLogService.LogSecurityEventAsync(
                    "password_change_failed",
                    "Failed password change attempt",
                    ex.Message
                );

                return OkException(ex);
            }
        }
    }
}
