namespace RateLimiterAPI.Models
{
    public class ClientRequestInfo
    {
        public int RequestCount { get; set; }
        public DateTime ResetTime { get; set; }
    }
}
