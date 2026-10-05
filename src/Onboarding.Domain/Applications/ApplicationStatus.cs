namespace Atlas.Onboarding.Domain.Applications;

/// <summary>Where an application is in its lifecycle. Transitions come with verification.</summary>
public enum ApplicationStatus
{
    /// <summary>Accepted and waiting for identity verification and sanctions screening.</summary>
    Submitted,

    Approved,

    Rejected,

    /// <summary>Possible sanctions or PEP match: a market compliance officer must decide (Compliance 3).</summary>
    Referred,

    /// <summary>
    /// Approved in a market where activation needs a branch visit and a wet signature (Annex B: MD). Remote
    /// identification covers the application only.
    /// </summary>
    AwaitingBranchVisit,

    /// <summary>Approved, and core banking has opened the current account.</summary>
    AccountOpened,
}
