using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AttechServer.Shared.WebAPIBase;
using AttechServer.Shared.ApplicationBase.Common;

namespace AttechServer.Controllers
{
    [Route("api")]
    [ApiController]
    public class CsrfController : ApiControllerBase
    {
        public CsrfController(ILogger<CsrfController> logger) : base(logger)
        {
        }

        /// <summary>
        /// Get CSRF token for authenticated requests
        /// </summary>
        [HttpGet("csrf-token")]
        [Authorize]
        public ApiResponse GetCsrfToken()
        {
            try
            {
                // Force generate CSRF token manually if middleware didn't do it
                if (!HttpContext.Response.Headers.ContainsKey("X-CSRF-Token"))
                {
                    var token = GenerateSecureToken();

                    // Set cookie
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = HttpContext.Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    };
                    HttpContext.Response.Cookies.Append("CSRF-TOKEN", token, cookieOptions);

                    // Set header
                    HttpContext.Response.Headers["X-CSRF-Token"] = token;
                }

                return new ApiResponse(ApiStatusCode.Success, new { message = "CSRF token generated" }, 200, "Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating CSRF token");
                return OkException(ex);
            }
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }
    }
}