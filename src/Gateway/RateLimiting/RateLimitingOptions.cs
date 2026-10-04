namespace Atlas.Gateway.RateLimiting;

/// <summary>Bound from "Gateway:RateLimiting".</summary>
public sealed class RateLimitingOptions
{
    public const string Section = "Gateway:RateLimiting";

    /// <summary>
    /// Off for integration tests, which submit many applications from one address. When off, the policy still exists
    /// (the routes refer to it) but limits nothing, and Redis is not used at all.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Submissions one client address may make per <see cref="Window"/>.</summary>
    public int SubmissionsPerWindow { get; init; } = 10;

    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}
