namespace Atlas.Accounts.Domain.Openings;

public enum EscalationReason
{
    /// <summary>Core banking refused the request (VALIDATION): the same data cannot succeed.</summary>
    RejectedByCoreBanking,

    /// <summary>The outcome of a call is unknown and the customer cannot be looked up (identified by passport).</summary>
    OutcomeCannotBeChecked,

    /// <summary>The outcome of a call is unknown and the account did not appear in any of the lookups.</summary>
    OutcomeNotConfirmed,
}
