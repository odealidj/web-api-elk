namespace web_api_elk.Services;

public interface IRateLimiter
{
    /// <summary>
    /// Returns true if the given key is allowed to perform the action.
    /// If false, retryAfter indicates how long to wait before trying again.
    /// </summary>
    bool IsAllowed(string key, out TimeSpan retryAfter);
}
