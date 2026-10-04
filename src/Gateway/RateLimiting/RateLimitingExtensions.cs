using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Atlas.Gateway.RateLimiting;

/// <summary>
/// Limits submissions per client address at the edge. Every submission costs real money (an IDNow identification, a
/// World-Check case) and stores two images, so a script resubmitting in a loop is a cost and abuse problem before it is
/// a load problem. Status reads are not limited: mobile polls them, and they are one primary-key lookup.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>The policy name the "submit-application" route refers to in the ReverseProxy configuration.</summary>
    private const string SubmissionsPolicy = "submissions";

    private const string RedisConnectionName = "redis";

    public static IHostApplicationBuilder AddSubmissionRateLimiting(this IHostApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection(RateLimitingOptions.Section).Get<RateLimitingOptions>() ?? new();
        builder.Services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.Section);

        if (settings.Enabled)
        {
            // No Redis health check: the limiter fails open, so a gateway without Redis still serves every request and
            // must stay in rotation. A Redis outage shows up as the limiter's warnings instead.
            builder.AddRedisClient(
                RedisConnectionName,
                redis => redis.DisableHealthChecks = true,
                redis =>
                {
                    // Every submission waits on this check, so a missing Redis must fail it at once, not after the
                    // client's default five-second timeout: commands are refused while disconnected, and one check may
                    // take a quarter of a second at most.
                    redis.BacklogPolicy = BacklogPolicy.FailFast;
                    redis.AsyncTimeout = 250;
                    redis.AbortOnConnectFail = false;
                });
        }

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(SubmissionsPolicy, context =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                if (!options.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }
                
                var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.Get(client, address => new RedisFixedWindowRateLimiter(
                    $"atlas:gateway:ratelimit:{SubmissionsPolicy}:{address}",
                    options.SubmissionsPerWindow,
                    options.Window,
                    context.RequestServices.GetRequiredService<IConnectionMultiplexer>(),
                    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<RedisFixedWindowRateLimiter>()));
            });

            limiter.OnRejected = async (rejected, cancellationToken) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    rejected.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many submissions",
                        detail: "Too many applications were submitted from this address. Try again after the Retry-After interval.")
                    .ExecuteAsync(rejected.HttpContext);
            };
        });

        return builder;
    }
}
