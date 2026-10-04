namespace Atlas.Accounts.Domain.Openings;

/// <summary>
/// Where an account opening stands. OpenAccount has no idempotency (CBS §3), so the states keep apart what is
/// certain from what is not: an opening is only ever called again when the previous call certainly did nothing.
/// </summary>
public enum OpeningStatus
{
    /// <summary>Waiting for its turn to call OpenAccount, at <see cref="AccountOpening.DueAt"/>.</summary>
    Queued,

    /// <summary>An OpenAccount call is in flight.</summary>
    Opening,

    /// <summary>
    /// An OpenAccount call ended without an answer: the account may or may not exist (CBS §4). Waiting until the
    /// lookup replica has caught up, at <see cref="AccountOpening.DueAt"/>.
    /// </summary>
    AwaitingConfirmation,

    /// <summary>A FindCustomerAccounts call is in flight to find out.</summary>
    Confirming,

    Opened,

    /// <summary>Handed to operations; nothing is retried automatically.</summary>
    Escalated,
}
