namespace RateLimiterAPI.Middleware
{
    using Microsoft.AspNetCore.Http;
    using RateLimiterAPI.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class RoleBasedRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Dictionary<string, ClientRequestInfo> _users = new();

        // Role-based limits (can come from a config or database)
        private static readonly Dictionary<string, (int Limit, TimeSpan Period)> RoleLimits = new()
    {
        { "Free", (Limit: 5, Period: TimeSpan.FromMinutes(1)) },
        { "Premium", (Limit: 20, Period: TimeSpan.FromMinutes(1)) },
        { "Admin", (Limit: 100, Period: TimeSpan.FromMinutes(1)) }
    };

        public RoleBasedRateLimitingMiddleware(RequestDelegate next)
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

           
            var userRole = context.Request.Headers["X-User-Role"].ToString() ?? "Free";

        
            if (!RoleLimits.TryGetValue(userRole, out var roleLimit))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync($"Invalid role: {userRole}");
                return;
            }

            var (limit, period) = roleLimit;

       
            if (!_users.TryGetValue(userId, out var userInfo))
            {
                userInfo = new ClientRequestInfo
                {
                    RequestCount = 0,
                    ResetTime = DateTime.UtcNow.Add(period)
                };
                _users[userId] = userInfo;
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

// For testing the Free role curl -X GET http://localhost:5000/api/test -H "X-User-ID: user1" -H "X-User-Role: Free"
// For testing the Preimum role curl -X GET http://localhost:5000/api/test -H "X-User-ID: user2" -H "X-User-Role: Premium"
// For testing the Admin role curl -X GET http://localhost:5000/api/test -H "X-User-ID: user3" -H "X-User-Role: Admin"
// For testing the Invalid role curl -X GET http://localhost:5000/api/test -H "X-User-ID: user4" -H "X-User-Role: InvalidRole"

