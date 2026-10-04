namespace Atlas.Accounts.Contracts;

/// <summary>
/// Published by Accounts when it cannot open an account, or cannot establish whether it did, without a person.
/// Operations take over from here; nothing is retried automatically after this.
/// </summary>
public sealed record AccountOpeningEscalated(
    Guid ApplicationId,
    string Market,
    EscalationReason Reason,
    string Detail,
    DateTimeOffset EscalatedAt);

public enum EscalationReason
{
    /// <summary>Core banking refused the request (VALIDATION): retrying the same data cannot succeed.</summary>
    RejectedByCoreBanking,

    /// <summary>
    /// The call ended without an answer and the outcome cannot be looked up: customers identified by passport
    /// cannot be found in core banking (CBS-4471).
    /// </summary>
    OutcomeCannotBeChecked,

    /// <summary>The call ended without an answer and the account did not appear in the lookups that followed.</summary>
    OutcomeNotConfirmed,
}
