namespace Atlas.Onboarding.Api.Features.Applications;

/// <summary>
/// Contract enum: what mobile is told. <c>PROCESSING</c> exists only here: the domain application is simply
/// submitted while identity verification and sanctions screening run.
/// </summary>
public enum ApplicationStatus
{
    Processing,
    Approved,
    Rejected,

    /// <summary>A compliance officer is reviewing the application; this can take up to 48 hours.</summary>
    Referred,

    /// <summary>Approved; the customer must visit a branch to sign before the account is activated (MD).</summary>
    AwaitingBranchVisit,

    /// <summary>Approved, and the current account is open.</summary>
    AccountOpened,
}

public static class ApplicationStatusMapping
{
    public static ApplicationStatus ToContract(this Domain.Applications.ApplicationStatus status) => status switch
    {
        Domain.Applications.ApplicationStatus.Submitted => ApplicationStatus.Processing,
        Domain.Applications.ApplicationStatus.Approved => ApplicationStatus.Approved,
        Domain.Applications.ApplicationStatus.Rejected => ApplicationStatus.Rejected,
        Domain.Applications.ApplicationStatus.Referred => ApplicationStatus.Referred,
        Domain.Applications.ApplicationStatus.AwaitingBranchVisit => ApplicationStatus.AwaitingBranchVisit,
        Domain.Applications.ApplicationStatus.AccountOpened => ApplicationStatus.AccountOpened,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>
    /// Whether the customer has a decision. Without one, mobile checks back later. An approved application moves on
    /// to <c>ACCOUNT_OPENED</c> once core banking has opened the account, typically within a few minutes.
    /// </summary>
    public static bool IsFinal(this ApplicationStatus status) =>
        status is ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.AwaitingBranchVisit
            or ApplicationStatus.AccountOpened;
}
