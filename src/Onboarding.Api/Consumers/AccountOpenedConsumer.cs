using Atlas.Accounts.Contracts;
using Atlas.Onboarding.Application.Features.Applications.RecordAccountOpened;
using MassTransit;
using MediatR;

namespace Atlas.Onboarding.Api.Consumers;

/// <summary>Records the account Accounts opened for an approved application. Maps the message to a command.</summary>
public sealed class AccountOpenedConsumer(ISender sender) : IConsumer<AccountOpened>
{
    public Task Consume(ConsumeContext<AccountOpened> context) =>
        sender.Send(
            new RecordAccountOpenedCommand(context.Message.ApplicationId, context.Message.AccountNumber, context.Message.OpenedAt),
            context.CancellationToken);
}
