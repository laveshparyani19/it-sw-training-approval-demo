using Microsoft.AspNetCore.Builder;

namespace ApprovalDemo.Api.Middleware
{
    /// <summary>
    /// Middleware to add security headers to all HTTP responses
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Prevent XSS attacks
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            // Prevent clickjacking - but allow framing from same origin
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

            // Content Security Policy - allow cross-origin API requests
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' https://it-sw-training-approval-backend.onrender.com";

            // Prevent MIME type sniffing
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            await _next(context);
        }
    }

    /// <summary>
    /// Extension method to add security headers middleware
    /// </summary>
    public static class SecurityHeadersExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}
