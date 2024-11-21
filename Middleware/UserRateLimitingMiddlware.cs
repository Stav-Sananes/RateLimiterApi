namespace RateLimiterAPI.Middleware
{
    using Microsoft.AspNetCore.Http;
    using RateLimiterAPI.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class UserRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, ClientRequestInfo> _users = new();
        private const int Limit = 5; // Max requests per period
        private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

        public UserRateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var userId = context.Request.Headers["X-User-ID"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("User ID is required.");
                return;
            }

            if (!_users.TryGetValue(userId, out var userInfo))
            {
                userInfo = new ClientRequestInfo
                {
                    RequestCount = 0,
                    ResetTime = DateTime.UtcNow.Add(Period)
                };
                _users[userId] = userInfo;
            }

            if (DateTime.UtcNow > userInfo.ResetTime)
            {
                userInfo.RequestCount = 0;
                userInfo.ResetTime = DateTime.UtcNow.Add(Period);
            }

            userInfo.RequestCount++;

            context.Response.Headers["X-RateLimit-Limit"] = Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (Limit - userInfo.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = userInfo.ResetTime.ToUniversalTime()
                                                                   .Subtract(DateTime.UnixEpoch)
                                                                   .TotalSeconds
                                                                   .ToString();

            if (userInfo.RequestCount > Limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Too many requests. Please try again later.");
                return;
            }

            await _next(context);
        }
    }



}
