namespace Atlas.Verification.Domain.Checks;

/// <summary>What sanctions and PEP screening (World-Check) found.</summary>
public sealed record ScreeningCheck(string CaseId, ScreeningStatus Status, IReadOnlyList<ScreeningMatch> Matches);

/// <summary>One possible match. The matched name is not kept: it is the applicant's own, already on record.</summary>
public sealed record ScreeningMatch(WatchList List, double Score);

public enum ScreeningStatus
{
    Clear,
    PossibleMatch,
}

public enum WatchList
{
    Sanctions,
    Pep,
}
