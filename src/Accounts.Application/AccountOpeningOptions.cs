namespace Atlas.Accounts.Application;

/// <summary>How account openings are paced, bound from "Accounts:Opening". Defaults follow the CBS integration page.</summary>
public sealed class AccountOpeningOptions
{
    public const string Section = "Accounts:Opening";

    /// <summary>
    /// This service's share of a market's 5 calls in flight (CBS §6), across all its instances. Branch systems
    /// share the same ceiling and use most of it in opening hours, so the channel takes few slots and leaves the rest.
    /// </summary>
    public int CallsInFlightPerMarket { get; init; } = 2;

    /// <summary>How long to wait for OpenAccount: typically 20-90 seconds, p99 longer in branch hours (CBS §2).</summary>
    public TimeSpan OpenAccountTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public TimeSpan LookupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Added to a call's timeout to make its lease: after that the call is presumed lost with its worker. Long enough
    /// that a live call always reports before its lease runs out.
    /// </summary>
    public TimeSpan LeaseMargin { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long after an unanswered OpenAccount to look the account up, and between lookups: the replica serving
    /// lookups shows a new account within 60 seconds (CBS §2, §4).
    /// </summary>
    public TimeSpan ConfirmationDelay { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Lookups before an unanswered opening goes to operations. An OpenAccount call can still be running in core
    /// banking after the client gave up, so one empty lookup does not prove nothing was opened.
    /// </summary>
    public int MaxConfirmationChecks { get; init; } = 5;

    /// <summary>How long to wait after CONCURRENCY_LIMIT or a failed connection; a random part is added to it.</summary>
    public TimeSpan BusyRetryDelay { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The shortest wait between two looks at the queue, while openings are due but the market's slots are taken.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The longest wait between two looks at the queue. The processor otherwise sleeps until the next opening is due or
    /// it is woken by work arriving in this instance; this bounds the wait for work another instance queued or freed.
    /// </summary>
    public TimeSpan IdlePollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to hold calls during the end-of-day window. Always on in production; off only to try the system out
    /// locally at night, together with the stand-in's own Stubs:CoreBanking:EnforceEndOfDay.
    /// </summary>
    public bool ObserveEndOfDayWindow { get; init; } = true;

    /// <summary>Each market's time zone (IANA id); the end-of-day window is in market time (CBS §5).</summary>
    public Dictionary<string, string> MarketTimeZones { get; init; } = [];

    public TimeZoneInfo TimeZoneOf(string market) =>
        MarketTimeZones.TryGetValue(market, out var zone)
            ? TimeZoneInfo.FindSystemTimeZoneById(zone)
            : throw new InvalidOperationException($"Market {market} has no time zone configured.");
}
