namespace Atlas.Stubs;

/// <summary>Bound from the "Stubs" configuration section, so scenarios can be tuned without code changes.</summary>
public sealed class StubOptions
{
    /// <summary>How long a "slow" scenario takes to answer.</summary>
    public TimeSpan SlowResponseDelay { get; init; } = TimeSpan.FromSeconds(20);
}
