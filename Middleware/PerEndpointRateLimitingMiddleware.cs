namespace RateLimiterAPI.Middleware
{
    using Microsoft.AspNetCore.Http;
    using RateLimiterAPI.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class PerEndpointRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, ClientRequestInfo> _users = new();

        private static readonly Dictionary<string, (int Limit, TimeSpan Period)> EndpointLimits = new()
    {
        { "/api/test", (Limit: 5, Period: TimeSpan.FromMinutes(1)) },
        { "/api/critical", (Limit: 2, Period: TimeSpan.FromMinutes(1)) },
        { "/api/general", (Limit: 10, Period: TimeSpan.FromMinutes(1)) }
    };

        public PerEndpointRateLimitingMiddleware(RequestDelegate next)
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

            var endpoint = context.Request.Path.ToString();

            if (!EndpointLimits.TryGetValue(endpoint, out var endpointLimit))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync($"Endpoint {endpoint} is not rate-limited.");
                return;
            }

            var (limit, period) = endpointLimit;

            var userEndpointKey = $"{userId}:{endpoint}";

            if (!_users.TryGetValue(userEndpointKey, out var userInfo))
            {
                userInfo = new ClientRequestInfo
                {
                    RequestCount = 0,
                    ResetTime = DateTime.UtcNow.Add(period)
                };
                _users[userEndpointKey] = userInfo;
            }

            if (DateTime.UtcNow > userInfo.ResetTime)
            {
                userInfo.RequestCount = 0;
                userInfo.ResetTime = DateTime.UtcNow.Add(period);
            }

            userInfo.RequestCount++;

            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (limit - userInfo.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = userInfo.ResetTime.ToUniversalTime()
                                                                   .Subtract(DateTime.UnixEpoch)
                                                                   .TotalSeconds
                                                                   .ToString();

            if (userInfo.RequestCount > limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Too many requests. Please try again later.");
                return;
            }

            await _next(context);
        }
    }

  

}
//# Test endpoint (limit: 5 requests/minute)
//curl - X GET http://localhost:5000/api/test -H "X-User-ID: user1"

//# Critical endpoint (limit: 2 requests/minute)
//curl - X GET http://localhost:5000/api/critical -H "X-User-ID: user1"

//# General endpoint (limit: 10 requests/minute)
//curl - X GET http://localhost:5000/api/general -H "X-User-ID: user1"