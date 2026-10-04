using Atlas.Accounts.Application.RequestAccountOpening;
using Atlas.Accounts.Domain.Openings;
using Atlas.Onboarding.Contracts;
using MassTransit;

namespace Atlas.Accounts.Worker.Consumers;

/// <summary>Queues an account for every application Onboarding approves. Maps the message to the use case, nothing else.</summary>
public sealed class AccountOpeningRequestedConsumer(RequestAccountOpeningHandler handler) : IConsumer<AccountOpeningRequested>
{
    public Task Consume(ConsumeContext<AccountOpeningRequested> context) =>
        handler.HandleAsync(ToCommand(context.Message), context.CancellationToken);

    private static RequestAccountOpeningCommand ToCommand(AccountOpeningRequested message) =>
        new(
            message.ApplicationId,
            message.Market,
            new Customer(
                message.FirstName,
                message.LastName,
                message.DateOfBirth,
                message.NationalId,
                message.Passport?.Number,
                message.Passport?.IssuingCountry));
}
