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
}
