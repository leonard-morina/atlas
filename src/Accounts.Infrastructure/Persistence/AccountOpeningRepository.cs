using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Domain.Openings;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Accounts.Infrastructure.Persistence;

internal sealed class AccountOpeningRepository(AccountsDbContext db) : IAccountOpeningRepository
{
    public Task<bool> ExistsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        db.Openings.AnyAsync(opening => opening.ApplicationId == applicationId, cancellationToken);

    public async Task<AccountOpening?> FindAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await db.Openings.FindAsync([applicationId], cancellationToken);

    public void Add(AccountOpening opening) => db.Openings.Add(opening);

    public Task<IReadOnlyList<Guid>> ClaimDueAsync(
        string market,
        int maxInFlight,
        DateTimeOffset now,
        Action<AccountOpening> start,
        CancellationToken cancellationToken) =>
        // A transaction the connection-retry strategy can repeat as a whole.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // One claim per market at a time, across every instance: the count of calls in flight below cannot change
            // between reading it and adding to it. Released with the transaction.
            await db.Database.ExecuteSqlAsync(
                $"""
                 DECLARE @result int;
                 EXEC @result = sp_getapplock @Resource = {"account-openings/" + market}, @LockMode = 'Exclusive',
                     @LockOwner = 'Transaction', @LockTimeout = 10000;
                 IF @result < 0 THROW 50000, 'The market claim lock was not granted.', 1;
                 """,
                cancellationToken);

            var inFlight = await db.Openings.CountAsync(
                opening => opening.Market == market
                           && (opening.Status == OpeningStatus.Opening || opening.Status == OpeningStatus.Confirming)
                           && opening.LeaseExpiresAt > now,
                cancellationToken);

            if (inFlight >= maxInFlight)
            {
                return (IReadOnlyList<Guid>)[];
            }

            var due = await db.Openings
                .Where(opening => opening.Market == market
                                  && (opening.Status == OpeningStatus.Queued || opening.Status == OpeningStatus.AwaitingConfirmation)
                                  && opening.DueAt <= now)
                .OrderBy(opening => opening.DueAt)
                .Take(maxInFlight - inFlight)
                .ToListAsync(cancellationToken);

            due.ForEach(start);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return due.Select(opening => opening.ApplicationId).ToList();
        });

    public async Task<OpeningSchedule> FindScheduleAsync(CancellationToken cancellationToken)
    {
        var nextDueByMarket = await db.Openings
            .Where(opening => opening.Status == OpeningStatus.Queued || opening.Status == OpeningStatus.AwaitingConfirmation)
            .GroupBy(opening => opening.Market)
            .Select(market => new { Market = market.Key, NextDue = market.Min(opening => opening.DueAt) })
            .ToDictionaryAsync(market => market.Market, market => market.NextDue, cancellationToken);

        var nextLeaseExpiry = await db.Openings
            .Where(opening => opening.Status == OpeningStatus.Opening || opening.Status == OpeningStatus.Confirming)
            .MinAsync(opening => opening.LeaseExpiresAt, cancellationToken);

        return new OpeningSchedule(nextDueByMarket, nextLeaseExpiry);
    }

    public async Task<IReadOnlyList<AccountOpening>> FindExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Openings
            .Where(opening => (opening.Status == OpeningStatus.Opening || opening.Status == OpeningStatus.Confirming)
                              && opening.LeaseExpiresAt <= now)
            .ToListAsync(cancellationToken);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
