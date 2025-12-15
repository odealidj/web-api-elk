using Microsoft.Extensions.Caching.Memory;

namespace web_api_elk.Services;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly IMemoryCache _cache;

    private readonly int _maxAttempts;
    private readonly TimeSpan _window;

    private class RateLimitEntry
    {
        public int Count { get; set; }
        public DateTime FirstAttemptUtc { get; set; }
    }

    public InMemoryRateLimiter(IMemoryCache cache, int maxAttempts = 5, TimeSpan? window = null)
    {
        _cache = cache;
        _maxAttempts = maxAttempts;
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    public bool IsAllowed(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        var now = DateTime.UtcNow;
        var entry = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = _window;
            return new RateLimitEntry
            {
                Count = 0,
                FirstAttemptUtc = now
            };
        });

        // reset window if expired but cache entry still present
        var elapsed = now - entry.FirstAttemptUtc;
        if (elapsed > _window)
        {
            entry.Count = 0;
            entry.FirstAttemptUtc = now;
            elapsed = TimeSpan.Zero;
        }

        if (entry.Count >= _maxAttempts)
        {
            retryAfter = _window - elapsed;
            if (retryAfter < TimeSpan.Zero)
            {
                retryAfter = TimeSpan.Zero;
            }
            return false;
        }

        entry.Count++;
        return true;
    }
}

