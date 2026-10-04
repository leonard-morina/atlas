using Atlas.Accounts.Contracts;
using Atlas.Accounts.Domain.Openings;
using MassTransit;
using EscalationReason = Atlas.Accounts.Domain.Openings.EscalationReason;

namespace Atlas.Accounts.Application.ProcessOpenings;

internal static class OpeningEvents
{
    /// <summary>
    /// Tells the other services when an opening is finished, opened or escalated. Stored by the outbox and saved with
    /// the opening, so the event exists exactly when the outcome does.
    /// </summary>
    public static async Task PublishCompletionAsync(this IPublishEndpoint publishEndpoint, AccountOpening opening)
    {
        switch (opening.Status)
        {
            case OpeningStatus.Opened:
                await publishEndpoint.Publish(new AccountOpened(
                    opening.ApplicationId, opening.Market, opening.AccountNumber!, opening.CompletedAt!.Value));
                break;
            case OpeningStatus.Escalated:
                await publishEndpoint.Publish(new AccountOpeningEscalated(
                    opening.ApplicationId,
                    opening.Market,
                    opening.EscalationReason switch
                    {
                        EscalationReason.RejectedByCoreBanking => Contracts.EscalationReason.RejectedByCoreBanking,
                        EscalationReason.OutcomeCannotBeChecked => Contracts.EscalationReason.OutcomeCannotBeChecked,
                        EscalationReason.OutcomeNotConfirmed => Contracts.EscalationReason.OutcomeNotConfirmed,
                        _ => throw new ArgumentOutOfRangeException(nameof(opening), opening.EscalationReason, null),
                    },
                    opening.EscalationDetail!,
                    opening.CompletedAt!.Value));
                break;
        }
    }
}
