using Microsoft.Extensions.Configuration;

namespace Atlas.Verification.Infrastructure.Providers;

/// <summary>One external provider's settings, bound from "Providers:{name}".</summary>
public sealed class ProviderOptions
{
    public const string IdNow = "IdNow";
    public const string WorldCheck = "WorldCheck";

    /// <summary>The provider's API root. The stand-ins locally (http://stubs/...), the real provider in production.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>How long one call may take before it is abandoned.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The longest a provider call can take, as configured. A stopping worker finishes the message in progress, which may
    /// be waiting on a provider, so its host waits this long, plus time to record the verdict.
    /// </summary>
    public static TimeSpan LongestTimeout(IConfiguration configuration) =>
        new[] { IdNow, WorldCheck }
            .Select(provider => configuration.GetSection($"Providers:{provider}").Get<ProviderOptions>()?.Timeout
                                ?? TimeSpan.FromSeconds(30))
            .Max();
}
