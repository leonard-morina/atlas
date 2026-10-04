using Atlas.Accounts.Application.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Application.ProcessOpenings;

/// <summary>
/// Finds calls whose worker stopped before they reported (a crash, a deployment) and moves their openings on:
/// a lost OpenAccount may have opened the account, so it is looked up; a lost lookup is made again.
/// </summary>
public sealed class RecoverLostCallsHandler(
    IAccountOpeningRepository openings,
    IPublishEndpoint publishEndpoint,
    IOptions<AccountOpeningOptions> options,
    TimeProvider time,
    ILogger<RecoverLostCallsHandler> logger)
{
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        foreach (var opening in await openings.FindExpiredLeasesAsync(now, cancellationToken))
        {
            logger.LogWarning(
                "Call for application {ApplicationId} was lost while {Status}", opening.ApplicationId, opening.Status);

            opening.RecoverExpiredLease(now, now + options.Value.ConfirmationDelay);
            await publishEndpoint.PublishCompletionAsync(opening);
        }

        // Another instance recovering the same openings at the same time: one of them wins, which is enough.
        await openings.SaveChangesAsync(cancellationToken);
    }
}
