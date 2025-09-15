using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace AttechServer.Shared.Middlewares
{
    public class CsrfProtectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CsrfProtectionMiddleware> _logger;
        private const string CSRF_TOKEN_HEADER = "X-CSRF-Token";
        private const string CSRF_TOKEN_COOKIE = "CSRF-TOKEN";

        public CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip CSRF protection for safe methods
            if (IsSafeMethod(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Skip for API authentication endpoints and swagger
            if (ShouldSkipCsrfProtection(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Skip if not authenticated (JWT will handle this)
            if (!context.User.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            // Generate CSRF token for GET requests
            if (context.Request.Method == "GET")
            {
                GenerateCsrfToken(context);
                await _next(context);
                return;
            }

            // Validate CSRF token for state-changing requests
            if (!await ValidateCsrfToken(context))
            {
                _logger.LogWarning("CSRF token validation failed for {Path} from {IP}",
                    context.Request.Path, context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("{\"error\":\"CSRF token validation failed\"}");
                return;
            }

            await _next(context);
        }

        private bool IsSafeMethod(string method)
        {
            return method == "GET" || method == "HEAD" || method == "OPTIONS";
        }

        private bool ShouldSkipCsrfProtection(PathString path)
        {
            var pathValue = path.Value?.ToLower() ?? "";

            // Skip for auth endpoints and swagger
            return pathValue.StartsWith("/api/auth/login") ||
                   pathValue.StartsWith("/api/auth/refresh-token") ||
                   pathValue.StartsWith("/swagger") ||
                   pathValue.StartsWith("/api/docs") ||
                   pathValue.StartsWith("/hubs/"); // SignalR hubs
        }

        private void GenerateCsrfToken(HttpContext context)
        {
            // Check if token already exists
            if (context.Request.Cookies.ContainsKey(CSRF_TOKEN_COOKIE))
            {
                return;
            }

            var token = GenerateSecureToken();

            // Set as HTTP-only cookie for security
            var cookieOptions = new CookieOptions
            {
                HttpOnly = false, // Frontend needs to read this to send in header
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            };

            context.Response.Cookies.Append(CSRF_TOKEN_COOKIE, token, cookieOptions);

            // Also add to response header for easier frontend access
            context.Response.Headers.Add(CSRF_TOKEN_HEADER, token);
        }

        private Task<bool> ValidateCsrfToken(HttpContext context)
        {
            // Get token from header
            var headerToken = context.Request.Headers[CSRF_TOKEN_HEADER].FirstOrDefault();

            // Get token from cookie
            var cookieToken = context.Request.Cookies[CSRF_TOKEN_COOKIE];

            // Both tokens must exist and match
            if (string.IsNullOrEmpty(headerToken) || string.IsNullOrEmpty(cookieToken))
            {
                return Task.FromResult(false);
            }

            // Constant-time comparison to prevent timing attacks
            return Task.FromResult(SecureEquals(headerToken, cookieToken));
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        private bool SecureEquals(string a, string b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }
    }

    public static class CsrfProtectionMiddlewareExtensions
    {
        public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CsrfProtectionMiddleware>();
        }
    }
}