using RateLimiterAPI.Models;

namespace RateLimiterAPI.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, ClientRequestInfo> _clients = new();
        private const int Limit = 5; 
        private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (clientIp == null)
            {
                await _next(context);
                return;
            }

            if (!_clients.TryGetValue(clientIp, out var clientInfo))
            {
                clientInfo = new ClientRequestInfo
                {
                    RequestCount = 0,
                    ResetTime = DateTime.UtcNow.Add(Period)
                };
                _clients[clientIp] = clientInfo;
            }

            if (DateTime.UtcNow > clientInfo.ResetTime)
            {
                clientInfo.RequestCount = 0;
                clientInfo.ResetTime = DateTime.UtcNow.Add(Period);
            }

            clientInfo.RequestCount++;

            context.Response.Headers["X-RateLimit-Limit"] = Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (Limit - clientInfo.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = clientInfo.ResetTime.ToUniversalTime()
                                                                       .Subtract(DateTime.UnixEpoch)
                                                                       .TotalSeconds
                                                                       .ToString();
            if (clientInfo.RequestCount > Limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Too many requests. Please try again later.");
                return;
            }

            await _next(context);
        }
    }

}
