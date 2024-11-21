namespace RateLimiterAPI.Middleware
{
    public class TokenBucketRateLimiter
    {
        private static readonly Dictionary<string, (int Tokens, DateTime LastRefill)> _buckets = new();
        private const int MaxTokens = 10;
        private const int RefillRate = 1; // 1 token per second

        public static bool AllowRequest(string clientKey)
        {
            if (!_buckets.TryGetValue(clientKey, out var bucket))
            {
                bucket = (MaxTokens, DateTime.UtcNow);
            }

            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - bucket.LastRefill).TotalSeconds;

            // Refill tokens
            var tokensToAdd = (int)(elapsedSeconds * RefillRate);
            bucket.Tokens = Math.Min(MaxTokens, bucket.Tokens + tokensToAdd);
            bucket.LastRefill = now;

            // Allow or block request
            if (bucket.Tokens > 0)
            {
                bucket.Tokens--;
                _buckets[clientKey] = bucket;
                return true;
            }

            return false;
        }
    }

}
