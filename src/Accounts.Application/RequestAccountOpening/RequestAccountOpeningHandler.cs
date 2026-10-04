using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Application.ProcessOpenings;
using Atlas.Accounts.Domain.Openings;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Application.RequestAccountOpening;

/// <summary>
/// Records that an account is to be opened; the processor makes the calls. Core banking is slow, closed at night and
/// shared with the branches, so the request is queued here rather than called from the message.
/// </summary>
public sealed class RequestAccountOpeningHandler(
    IAccountOpeningRepository openings,
    IOptions<AccountOpeningOptions> options,
    IPublishEndpoint publishEndpoint,
    TimeProvider time,
    ILogger<RequestAccountOpeningHandler> logger)
{
    public async Task HandleAsync(RequestAccountOpeningCommand command, CancellationToken cancellationToken)
    {
        // The inbox skips a redelivered message; this also covers the same application in a different message,
        // because an application gets one account, ever. The primary key settles a race between the two.
        if (await openings.ExistsAsync(command.ApplicationId, cancellationToken))
        {
            logger.LogInformation("Account opening for application {ApplicationId} is already requested", command.ApplicationId);
            return;
        }

        // A market without a time zone cannot be scheduled: a configuration fault for the error queue, not a skip.
        _ = options.Value.TimeZoneOf(command.Market);

        openings.Add(AccountOpening.Request(command.ApplicationId, command.Market, command.Customer, time.GetUtcNow()));

        // Wakes a processor to start it now rather than at its next look. Through the outbox, so it arrives only once the
        // opening is committed: woken any earlier, a processor would not see it yet (read committed snapshot).
        await publishEndpoint.Publish(new AccountOpeningQueued(command.ApplicationId, command.Market), cancellationToken);
        await openings.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Account opening for application {ApplicationId} queued in market {Market}", command.ApplicationId, command.Market);
    }
}
