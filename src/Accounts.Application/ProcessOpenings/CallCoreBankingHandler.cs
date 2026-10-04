using Atlas.Accounts.Application.Abstractions;
using Atlas.Accounts.Domain.Markets;
using Atlas.Accounts.Domain.Openings;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Accounts.Application.ProcessOpenings;

/// <summary>
/// Makes the call a claimed opening is waiting for, OpenAccount or the lookup that follows an unanswered one, and
/// records what it means. The outcome and its event are saved together (outbox).
/// </summary>
public sealed class CallCoreBankingHandler(
    IAccountOpeningRepository openings,
    ICoreBanking coreBanking,
    IPublishEndpoint publishEndpoint,
    IOptions<AccountOpeningOptions> options,
    TimeProvider time,
    ILogger<CallCoreBankingHandler> logger)
{
    private static readonly TimeSpan EndOfDayRecheck = TimeSpan.FromMinutes(1);

    private AccountOpeningOptions Options => options.Value;

    public async Task HandleAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var opening = await openings.FindAsync(applicationId, CancellationToken.None);
        if (opening is not { IsInFlight: true })
        {
            return;
        }

        if (opening.Status == OpeningStatus.Opening)
        {
            Record(opening, await coreBanking.OpenAccountAsync(
                opening.Market, opening.Customer, opening.ChannelReference, cancellationToken));
        }
        else
        {
            Record(opening, await coreBanking.FindCustomerAccountsAsync(
                opening.Market, opening.Customer.NationalId!, cancellationToken));
        }

        await publishEndpoint.PublishCompletionAsync(opening);

        // The call happened: its outcome is recorded even when the worker is stopping.
        if (!await openings.SaveChangesAsync(CancellationToken.None))
        {
            // The lease ran out before the call ended and another worker took over; its lookup finds this outcome.
            logger.LogWarning(
                "Account opening for application {ApplicationId} was taken over while its call ran", applicationId);
            return;
        }

        logger.LogInformation(
            "Account opening for application {ApplicationId}: {Status}", applicationId, opening.Status);
    }

    private void Record(AccountOpening opening, CoreBankingResult<string> result)
    {
        var now = time.GetUtcNow();

        switch (result)
        {
            case CoreBankingResult<string>.Answered(var accountNumber):
                opening.RecordOpened(accountNumber, now);
                break;
            case CoreBankingResult<string>.Faulted { Fault: CoreBankingFault.Validation } fault:
                opening.RecordRejected(fault.Message, now);
                break;
            case CoreBankingResult<string>.Faulted { Fault: CoreBankingFault.EndOfDayInProgress }:
                opening.Defer(AfterEndOfDay(opening.Market, now));
                break;
            case CoreBankingResult<string>.Faulted { Fault: CoreBankingFault.ConcurrencyLimit }:
            case CoreBankingResult<string>.NotDelivered:
                opening.Defer(AfterBusy(now));
                break;
            case CoreBankingResult<string>.Unanswered(var detail):
                logger.LogWarning(
                    "OpenAccount for application {ApplicationId} went unanswered: {Detail}", opening.ApplicationId, detail);
                opening.RecordOutcomeUnknown(detail, now, now + Options.ConfirmationDelay);
                break;
        }
    }

    private void Record(AccountOpening opening, CoreBankingResult<IReadOnlyList<CustomerAccount>> result)
    {
        var now = time.GetUtcNow();

        switch (result)
        {
            // Only the account this service opened counts: the customer may hold others, opened at a branch.
            case CoreBankingResult<IReadOnlyList<CustomerAccount>>.Answered(var accounts)
                when accounts.FirstOrDefault(account => account.ChannelReference == opening.ChannelReference) is { } ours:
                opening.RecordOpened(ours.AccountNumber, now);
                break;
            case CoreBankingResult<IReadOnlyList<CustomerAccount>>.Answered:
                opening.RecordNotFound(now, now + Options.ConfirmationDelay, Options.MaxConfirmationChecks);
                break;
            case CoreBankingResult<IReadOnlyList<CustomerAccount>>.Faulted { Fault: CoreBankingFault.Validation } fault:
                opening.RecordRejected(fault.Message, now);
                break;
            case CoreBankingResult<IReadOnlyList<CustomerAccount>>.Faulted { Fault: CoreBankingFault.EndOfDayInProgress }:
                opening.Defer(AfterEndOfDay(opening.Market, now));
                break;
            // A lookup changes nothing, so any other failure is simply tried again.
            default:
                opening.Defer(AfterBusy(now));
                break;
        }
    }

    // Not before the window ends. If our clock says the market is open, the market's own refusal wins: the window may
    // have started early or run late, so ask again in a minute rather than hammer it.
    private DateTimeOffset AfterEndOfDay(string market, DateTimeOffset now)
    {
        var reopens = EndOfDayWindow.NextOpening(now, Options.TimeZoneOf(market));
        return reopens > now ? reopens : now + EndOfDayRecheck;
    }

    // Spread out, so openings refused together do not all come back at the same moment.
    private DateTimeOffset AfterBusy(DateTimeOffset now) =>
        now + Options.BusyRetryDelay * (1 + Random.Shared.NextDouble() / 2);
}
