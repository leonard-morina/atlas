namespace Atlas.Verification.Domain.Checks;

/// <summary>What identity verification (IDNow) concluded about the document and the selfie.</summary>
public sealed record IdentityCheck(string IdentificationId, DocumentResult DocumentResult, bool FaceMatch, double Confidence);

public enum DocumentResult
{
    Valid,
    Invalid,
    Inconclusive,
}
