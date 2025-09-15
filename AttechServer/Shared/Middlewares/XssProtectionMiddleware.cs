using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace AttechServer.Shared.Middlewares
{
    public class XssProtectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<XssProtectionMiddleware> _logger;

        // Common XSS patterns to detect
        private static readonly Regex[] XssPatterns = new[]
        {
            new Regex(@"<\s*script[^>]*>.*?</\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(@"javascript\s*:", RegexOptions.IgnoreCase),
            new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase),
            new Regex(@"<\s*iframe[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<\s*object[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<\s*embed[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<\s*link[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<\s*meta[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"vbscript\s*:", RegexOptions.IgnoreCase),
            new Regex(@"data\s*:", RegexOptions.IgnoreCase),
            new Regex(@"expression\s*\(", RegexOptions.IgnoreCase)
        };

        public XssProtectionMiddleware(RequestDelegate next, ILogger<XssProtectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only scan POST, PUT, PATCH requests with content
            if (ShouldScanRequest(context.Request))
            {
                await ProcessRequestBody(context);
            }

            await _next(context);
        }

        private bool ShouldScanRequest(HttpRequest request)
        {
            var method = request.Method.ToUpper();
            return (method == "POST" || method == "PUT" || method == "PATCH") &&
                   request.ContentLength > 0 &&
                   request.ContentType?.Contains("application/json") == true;
        }

        private async Task ProcessRequestBody(HttpContext context)
        {
            var request = context.Request;

            // Enable buffering to allow multiple reads
            request.EnableBuffering();

            try
            {
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();

                // Reset position for next middleware
                request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    if (ContainsSuspiciousContent(body))
                    {
                        _logger.LogWarning("XSS attempt detected from {IP} to {Path}. Body: {Body}",
                            context.Connection.RemoteIpAddress,
                            request.Path,
                            body.Length > 200 ? body.Substring(0, 200) + "..." : body);

                        await ReturnXssBlockedResponse(context);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request body for XSS detection");
                // Continue processing on error to avoid breaking the application
            }
        }

        private bool ContainsSuspiciousContent(string content)
        {
            try
            {
                // URL decode the content to catch encoded XSS
                var decodedContent = HttpUtility.UrlDecode(content);
                var htmlDecodedContent = HttpUtility.HtmlDecode(decodedContent);

                // Check all variations
                var contentVariations = new[] { content, decodedContent, htmlDecodedContent };

                foreach (var variation in contentVariations)
                {
                    if (CheckPatterns(variation))
                    {
                        return true;
                    }
                }

                // Additional check for JSON content
                if (IsJsonContent(content))
                {
                    return CheckJsonForXss(content);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking content for XSS patterns");
                return false;
            }
        }

        private bool CheckPatterns(string content)
        {
            foreach (var pattern in XssPatterns)
            {
                if (pattern.IsMatch(content))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsJsonContent(string content)
        {
            try
            {
                JsonDocument.Parse(content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckJsonForXss(string jsonContent)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonContent);
                return CheckJsonElement(document.RootElement);
            }
            catch
            {
                return false;
            }
        }

        private bool CheckJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return CheckPatterns(element.GetString() ?? "");

                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (CheckJsonElement(property.Value))
                        {
                            return true;
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (CheckJsonElement(item))
                        {
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }

        private async Task ReturnXssBlockedResponse(HttpContext context)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Request blocked",
                message = "Potentially malicious content detected",
                statusCode = 400
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }

    public static class XssProtectionMiddlewareExtensions
    {
        public static IApplicationBuilder UseXssProtection(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<XssProtectionMiddleware>();
        }
    }
}