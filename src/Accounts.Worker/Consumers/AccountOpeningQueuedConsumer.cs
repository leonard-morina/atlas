using Atlas.Accounts.Application.ProcessOpenings;
using MassTransit;

namespace Atlas.Accounts.Worker.Consumers;

/// <summary>Wakes this instance's processor for an opening just queued (a broadcast: every instance receives it).</summary>
public sealed class AccountOpeningQueuedConsumer(OpeningSignal signal) : IConsumer<AccountOpeningQueued>
{
    public Task Consume(ConsumeContext<AccountOpeningQueued> context)
    {
        signal.Notify();
        return Task.CompletedTask;
    }
}
