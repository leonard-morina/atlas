using Atlas.Onboarding.Application.Features.Applications.GetApplication;
using MediatR;
using Microsoft.Extensions.Options;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>Bound from "Onboarding:DecisionWait".</summary>
public sealed class DecisionWaitOptions
{
    /// <summary>How long a submission waits for verification before answering 202 PROCESSING.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>
/// Mobile wants one call in and one answer out. Verification normally answers within a couple of seconds, so the
/// submission waits for the decision for a short, configured time; past that it answers PROCESSING and the app
/// checks <c>GET /applications/{id}</c> later (a slow provider, a 48-hour compliance review).
/// It polls the database instead of waiting for an in-process signal: with several replicas, the decision may be
/// recorded by a different instance from the one holding the request.
/// </summary>
public sealed class DecisionWait(ISender sender, IOptions<DecisionWaitOptions> options, TimeProvider time)
{
    public async Task<ApplicationState?> ForDecisionAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + options.Value.Budget;
        ApplicationState? state;

        while ((state = await sender.Send(new GetApplicationQuery(applicationId), cancellationToken)) is { DecidedAt: null }
               && time.GetUtcNow() < deadline)
        {
            await Task.Delay(options.Value.PollInterval, time, cancellationToken);
        }

        return state;
    }
}
