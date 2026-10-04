using Atlas.Accounts.Domain.Openings;

namespace Atlas.Accounts.Application.Abstractions;

public interface IAccountOpeningRepository
{
    Task<bool> ExistsAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<AccountOpening?> FindAsync(Guid applicationId, CancellationToken cancellationToken);

    void Add(AccountOpening opening);

    /// <summary>
    /// Claims the market's due openings, as many as its free call slots allow, and starts each with
    /// <paramref name="start"/>. Atomic across every instance of this service: two workers never claim the same
    /// opening, and together never have more than <paramref name="maxInFlight"/> calls in flight in the market.
    /// </summary>
    /// <returns>The claimed openings' application ids.</returns>
    Task<IReadOnlyList<Guid>> ClaimDueAsync(
        string market,
        int maxInFlight,
        DateTimeOffset now,
        Action<AccountOpening> start,
        CancellationToken cancellationToken);

    /// <summary>When there is work next: the earliest due opening per market, and the earliest lease to run out.</summary>
    Task<OpeningSchedule> FindScheduleAsync(CancellationToken cancellationToken);

    /// <summary>Openings whose call is presumed lost: in flight, with the lease run out.</summary>
    Task<IReadOnlyList<AccountOpening>> FindExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <returns><c>false</c> when another worker changed the opening first; nothing was saved.</returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <param name="NextDueByMarket">Markets with openings waiting for a call, and when the first of them is due.</param>
/// <param name="NextLeaseExpiry">When the first call in flight is presumed lost; <c>null</c> when none is in flight.</param>
public sealed record OpeningSchedule(IReadOnlyDictionary<string, DateTimeOffset> NextDueByMarket, DateTimeOffset? NextLeaseExpiry);
