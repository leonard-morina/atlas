using Atlas.Onboarding.Api.Features.Applications.SubmitApplication;
using Atlas.Onboarding.Contracts;
using MassTransit;

namespace Atlas.Onboarding.Api.Consumers;

/// <summary>
/// Wakes a submission waiting on this instance for its decision. A broadcast: every instance of this API receives the
/// event, because the decision may be recorded by one instance while the HTTP request waits on another. Onboarding
/// consumes its own event because the outbox delivers it only after the decision is committed: woken any earlier, the
/// submission could read the application before the decision is visible.
/// </summary>
public sealed class ApplicationDecidedConsumer(DecisionSignal signal) : IConsumer<ApplicationDecided>
{
    public Task Consume(ConsumeContext<ApplicationDecided> context)
    {
        signal.Decided(context.Message.ApplicationId);
        return Task.CompletedTask;
    }
}
