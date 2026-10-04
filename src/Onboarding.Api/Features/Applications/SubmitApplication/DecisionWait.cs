using Atlas.Onboarding.Application.Features.Applications.GetApplication;
using MediatR;
using Microsoft.Extensions.Options;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>Bound from "Onboarding:DecisionWait".</summary>
public sealed class DecisionWaitOptions
{
    /// <summary>How long a submission waits for verification before answering 202 PROCESSING.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How often the database is read anyway, in case the announcement is lost (the broker restarting, this instance's
    /// queue being recreated). Normally the announcement arrives first and this never fires.
    /// </summary>
    public TimeSpan FallbackPollInterval { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Mobile wants one call in and one answer out. Verification normally answers within a couple of seconds, so the
/// submission waits for the decision for a short, configured time; past that it answers PROCESSING and the app
/// checks <c>GET /applications/{id}</c> later (a slow provider, a 48-hour compliance review).
/// It sleeps until the decision is announced (<see cref="DecisionSignal"/>), then reads it from the database.
/// </summary>
public sealed class DecisionWait(
    ISender sender,
    DecisionSignal signal,
    IOptions<DecisionWaitOptions> options,
    TimeProvider time)
{
    public async Task<ApplicationState?> ForDecisionAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + options.Value.Budget;

        // Listening before the first read: a decision announced between the two is not missed.
        var decided = signal.WhenDecided(applicationId);
        try
        {
            ApplicationState? state;
            while ((state = await sender.Send(new GetApplicationQuery(applicationId), cancellationToken)) is { DecidedAt: null }
                   && time.GetUtcNow() < deadline)
            {
                var fallback = Task.Delay(Min(options.Value.FallbackPollInterval, deadline - time.GetUtcNow()), time, cancellationToken);

                // Once announced, the next read finds the decision; waiting on it again would return at once.
                await (decided.IsCompleted ? fallback : Task.WhenAny(decided, fallback));
                cancellationToken.ThrowIfCancellationRequested();
            }

            return state;
        }
        finally
        {
            signal.Forget(applicationId);
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}
