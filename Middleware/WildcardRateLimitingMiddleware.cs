namespace RateLimiterAPI.Middleware
{
    using Microsoft.AspNetCore.Http;
    using RateLimiterAPI.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class WildcardRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, ClientRequestInfo> _users = new();

        // Wildcard-based endpoint limits
        private static readonly Dictionary<string, (int Limit, TimeSpan Period)> EndpointLimits = new()
    {
        { "/api/users/*", (Limit: 10, Period: TimeSpan.FromMinutes(1)) },
        { "/api/products/*", (Limit: 5, Period: TimeSpan.FromMinutes(1)) },
        { "/api/*", (Limit: 20, Period: TimeSpan.FromMinutes(1)) } // Catch-all wildcard
    };

        public WildcardRateLimitingMiddleware(RequestDelegate next)
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

            var path = context.Request.Path.ToString();

            var matchingPattern = FindMatchingPattern(path);
            if (matchingPattern == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync($"No rate limits configured for endpoint: {path}");
                return;
            }

            var (limit, period) = EndpointLimits[matchingPattern];

            var userPatternKey = $"{userId}:{matchingPattern}";

            if (!_users.TryGetValue(userPatternKey, out var userInfo))
            {
                userInfo = new ClientRequestInfo
                {
                    RequestCount = 0,
                    ResetTime = DateTime.UtcNow.Add(period)
                };
                _users[userPatternKey] = userInfo;
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

        private string? FindMatchingPattern(string path)
        {
            return EndpointLimits.Keys
                .OrderByDescending(pattern => pattern.Count(c => c == '/')) 
                .FirstOrDefault(pattern => IsMatch(pattern, path));
        }

        private bool IsMatch(string pattern, string path)
        {
            var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (patternSegments.Length > pathSegments.Length && !pattern.EndsWith("*"))
            {
                return false;
            }

            for (int i = 0; i < patternSegments.Length; i++)
            {
                if (patternSegments[i] == "*")
                    return true; 

                if (i >= pathSegments.Length || patternSegments[i] != pathSegments[i])
                    return false; 
            }

            return true;
        }
    }



}
// /api/users/* (10 requests/minute):
//curl -X GET http://localhost:5000/api/users/view -H "X-User-ID: user1"
//curl - X GET http://localhost:5000/api/users/create -H "X-User-ID: user1"

// / api / products/* (5 requests/minute):
// curl -X GET http://localhost:5000/api/products/view -H "X-User-ID: user1"
//curl - X GET http://localhost:5000/api/products/add -H "X-User-ID: user1"

// /api/* (20 requests/minute - catch-all):
//curl - X GET http://localhost:5000/api/general/info -H "X-User-ID: user1"

// No Match (404)
//curl -X GET http://localhost:5000/api/unknown -H "X-User-ID: user1"
