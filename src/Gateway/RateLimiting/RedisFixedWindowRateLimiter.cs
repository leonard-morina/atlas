using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Atlas.Gateway.RateLimiting;

/// <summary>
/// A fixed window counted in Redis, so the limit holds across every gateway instance: one counter per client, created by
/// the first request and expiring with its window. If Redis cannot be reached the request is let through (fails open):
/// a cache outage must not become an onboarding outage, and the services behind stay protected by their own limits
/// (core banking's ceiling, the providers' quotas).
/// </summary>
internal sealed class RedisFixedWindowRateLimiter(
    string key,
    int permitLimit,
    TimeSpan window,
    IConnectionMultiplexer redis,
    ILogger logger) : RateLimiter
{
    // Atomic: the count and the window's expiry are set together, and the remaining time comes back for Retry-After.
    private static readonly LuaScript Count = LuaScript.Prepare(
        """
        local count = redis.call('INCR', @key)
        if count == 1 then redis.call('PEXPIRE', @key, @window) end
        return { count, redis.call('PTTL', @key) }
        """);

    // The state lives in Redis, so the in-process partition can be dropped whenever the framework likes.
    public override TimeSpan? IdleDuration => TimeSpan.Zero;

    public override RateLimiterStatistics? GetStatistics() => null;

    // Answering needs a round trip to Redis, so the synchronous attempt never grants; the middleware then asks
    // AcquireAsync, which does.
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Lease.NotYet;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(
                Count, new { key = (RedisKey)key, window = (long)window.TotalMilliseconds }))!;

            var count = (long)result[0];
            return count <= permitLimit ? Lease.Granted : Lease.Rejected(TimeSpan.FromMilliseconds(Math.Max((long)result[1], 0)));
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Rate limiting is unavailable; letting the request through");
            return Lease.Granted;
        }
    }

    private sealed class Lease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public static readonly Lease Granted = new(true, null);

        public static readonly Lease NotYet = new(false, null);

        public static Lease Rejected(TimeSpan retryAfter) => new(false, retryAfter);

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
            return metadata is not null;
        }
    }
}
