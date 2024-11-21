using StackExchange.Redis;

namespace RateLimiterAPI.Middleware
{
    public class RedisRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDatabase _redisDb;
        private const int Limit = 5;
        private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);


        public RedisRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _redisDb = redis.GetDatabase();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (clientIp == null)
            {
                await _next(context);
                return;
            }

            var key = $"RateLimit:{clientIp}";
            var requests = await _redisDb.StringIncrementAsync(key);

            if (requests == 1)
            {
                await _redisDb.KeyExpireAsync(key, Period);
            }
            var remainingRequests = Limit - (int)requests;
            var ttl = await _redisDb.KeyTimeToLiveAsync(key);
            context.Response.Headers["X-RateLimit-Limit"] = Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(remainingRequests, 0).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = DateTime.UtcNow.Add(ttl ?? TimeSpan.Zero)
                                                                          .Subtract(DateTime.UnixEpoch)
                                                                          .TotalSeconds
                                                                          .ToString();
            if (requests > Limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Too many requests. Please try again later.");
                return;
            }

     

            await _next(context);
        }
    }

}
