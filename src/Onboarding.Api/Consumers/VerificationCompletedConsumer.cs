using Atlas.Onboarding.Application.Features.Applications.RecordVerification;
using Atlas.Onboarding.Domain.Applications;
using Atlas.Verification.Contracts;
using MassTransit;
using MediatR;

namespace Atlas.Onboarding.Api.Consumers;

/// <summary>
/// Records Verification's decision on an application. An entry point like an endpoint: it maps the message to a
/// command; the inbox makes a redelivered message a no-op.
/// </summary>
public sealed class VerificationCompletedConsumer(ISender sender) : IConsumer<VerificationCompleted>
{
    public Task Consume(ConsumeContext<VerificationCompleted> context) =>
        sender.Send(
            new RecordVerificationCommand(context.Message.ApplicationId, ToVerdict(context.Message.Outcome), context.Message.CompletedAt),
            context.CancellationToken);

    private static VerificationVerdict ToVerdict(VerificationOutcome outcome) => outcome switch
    {
        VerificationOutcome.Approved => VerificationVerdict.Approved,
        VerificationOutcome.Rejected => VerificationVerdict.Rejected,
        VerificationOutcome.Referred => VerificationVerdict.Referred,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
