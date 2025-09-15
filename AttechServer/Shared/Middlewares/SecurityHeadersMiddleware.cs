using Microsoft.AspNetCore.Http;

namespace AttechServer.Shared.Middlewares
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityHeadersMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public SecurityHeadersMiddleware(
            RequestDelegate next,
            ILogger<SecurityHeadersMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Add security headers
            AddSecurityHeaders(context);

            await _next(context);
        }

        private void AddSecurityHeaders(HttpContext context)
        {
            var response = context.Response;
            var isProduction = _configuration.GetValue<bool>("IsProduction");

            // Prevent clickjacking
            if (!response.Headers.ContainsKey("X-Frame-Options"))
            {
                response.Headers.Add("X-Frame-Options", "DENY");
            }

            // Prevent MIME type sniffing
            if (!response.Headers.ContainsKey("X-Content-Type-Options"))
            {
                response.Headers.Add("X-Content-Type-Options", "nosniff");
            }

            // XSS Protection (for older browsers)
            if (!response.Headers.ContainsKey("X-XSS-Protection"))
            {
                response.Headers.Add("X-XSS-Protection", "1; mode=block");
            }

            // Referrer Policy
            if (!response.Headers.ContainsKey("Referrer-Policy"))
            {
                response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // Content Security Policy
            if (!response.Headers.ContainsKey("Content-Security-Policy"))
            {
                var csp = BuildContentSecurityPolicy(isProduction);
                response.Headers.Add("Content-Security-Policy", csp);
            }

            // Strict Transport Security (only in production with HTTPS)
            if (isProduction && context.Request.IsHttps && !response.Headers.ContainsKey("Strict-Transport-Security"))
            {
                response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
            }

            // Permissions Policy (formerly Feature Policy)
            if (!response.Headers.ContainsKey("Permissions-Policy"))
            {
                response.Headers.Add("Permissions-Policy",
                    "camera=(), microphone=(), geolocation=(), payment=()");
            }

            // Remove server header for security
            response.Headers.Remove("Server");
            response.Headers.Remove("X-Powered-By");
            response.Headers.Remove("X-AspNet-Version");
            response.Headers.Remove("X-AspNetMvc-Version");
        }

        private string BuildContentSecurityPolicy(bool isProduction)
        {
            if (isProduction)
            {
                // Strict CSP for production
                return "default-src 'self'; " +
                       "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                       "style-src 'self' 'unsafe-inline'; " +
                       "img-src 'self' data: https:; " +
                       "font-src 'self' data:; " +
                       "connect-src 'self'; " +
                       "media-src 'self'; " +
                       "object-src 'none'; " +
                       "frame-ancestors 'none'; " +
                       "base-uri 'self'; " +
                       "form-action 'self';";
            }
            else
            {
                // More permissive CSP for development
                return "default-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                       "img-src 'self' data: https: http:; " +
                       "connect-src 'self' ws: wss:; " +
                       "frame-ancestors 'none';";
            }
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}