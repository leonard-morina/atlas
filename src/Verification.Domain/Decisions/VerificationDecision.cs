using Atlas.Verification.Domain.Checks;

namespace Atlas.Verification.Domain.Decisions;

public sealed record VerificationDecision(Outcome Outcome, IReadOnlyList<Reason> Reasons)
{
    /// <summary>
    /// The decision table. A possible sanctions or PEP match is always referred, whatever identity verification
    /// found: Compliance 3 forbids automating or bypassing that review, and rejecting on document grounds while
    /// a match is open would decide the case without the officer. Otherwise any identity failure rejects.
    /// All reasons are kept, so an officer sees document problems alongside the match.
    /// </summary>
    public static VerificationDecision Decide(IdentityCheck identity, ScreeningCheck screening)
    {
        var reasons = new List<Reason>();

        switch (identity.DocumentResult)
        {
            case DocumentResult.Invalid:
                reasons.Add(Reason.DocumentInvalid);
                break;

            // Not defined anywhere in the requirements; treated as a rejection the customer can retry (open question).
            case DocumentResult.Inconclusive:
                reasons.Add(Reason.DocumentInconclusive);
                break;
        }

        if (!identity.FaceMatch)
        {
            reasons.Add(Reason.FaceMismatch);
        }

        if (screening.Status == ScreeningStatus.PossibleMatch)
        {
            // A possible match without details is still a possible match: treat it as the stricter list.
            reasons.AddRange(screening.Matches.Count == 0
                ? [Reason.PossibleSanctionsMatch]
                : screening.Matches
                    .Select(match => match.List == WatchList.Pep ? Reason.PossiblePepMatch : Reason.PossibleSanctionsMatch)
                    .Distinct());

            return new VerificationDecision(Outcome.Referred, reasons);
        }

        return new VerificationDecision(reasons.Count == 0 ? Outcome.Approved : Outcome.Rejected, reasons);
    }
}

public enum Outcome
{
    Approved,
    Rejected,
    Referred,
}

public enum Reason
{
    DocumentInvalid,
    DocumentInconclusive,
    FaceMismatch,
    PossibleSanctionsMatch,
    PossiblePepMatch,
}
