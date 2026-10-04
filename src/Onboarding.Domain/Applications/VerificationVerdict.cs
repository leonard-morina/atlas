namespace Atlas.Onboarding.Domain.Applications;

/// <summary>What identity verification and sanctions screening decided, as Onboarding needs to know it.</summary>
public enum VerificationVerdict
{
    Approved,
    Rejected,
    /// <summary>A possible sanctions or PEP match: a compliance officer decides.</summary>
    Referred,
}
