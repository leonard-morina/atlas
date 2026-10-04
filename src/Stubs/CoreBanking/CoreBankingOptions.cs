namespace Atlas.Stubs.CoreBanking;

/// <summary>
/// Bound from "Stubs:CoreBanking". The defaults follow the CBS integration page (ACCOUNTS v3.1) where that is
/// practical, and are shorter where waiting for real would only slow a demo down.
/// </summary>
public sealed class CoreBankingOptions
{
    /// <summary>How long OpenAccount takes. The real service takes 20-90 seconds.</summary>
    public TimeSpan OpenAccountDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long the "Timeout" scenario takes: longer than any client waits, but the account is opened.</summary>
    public TimeSpan TimeoutScenarioDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>How long before an opened account shows in FindCustomerAccounts. The real replica: up to 60 seconds.</summary>
    public TimeSpan ReplicaDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Calls in flight per market across all callers before the gateway answers CONCURRENCY_LIMIT (§6).</summary>
    public int CallCeilingPerMarket { get; init; } = 5;

    /// <summary>Capacity the branches are using right now, per market (§6: "most of this capacity").</summary>
    public int BranchCallsInFlight { get; init; }

    /// <summary>Whether calls between 22:00 and 06:00 market time are refused with EOD_IN_PROGRESS (§5).</summary>
    public bool EnforceEndOfDay { get; init; } = true;

    /// <summary>Each market's time zone (IANA id); the end-of-day window is in market time.</summary>
    public Dictionary<string, string> MarketTimeZones { get; init; } = [];
}
