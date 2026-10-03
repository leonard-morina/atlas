using Atlas.Verification.Domain.Checks;
using Atlas.Verification.Domain.Decisions;

namespace Atlas.Verification.UnitTests;

/// <summary>The decision table: what identity verification and screening together decide.</summary>
[TestClass]
public sealed class VerificationDecisionTests
{
    private static readonly IdentityCheck ValidIdentity = new("idn_1", DocumentResult.Valid, FaceMatch: true, 0.97);
    private static readonly ScreeningCheck Clear = new("wc_1", ScreeningStatus.Clear, []);
    private static readonly ScreeningCheck SanctionsMatch =
        new("wc_1", ScreeningStatus.PossibleMatch, [new ScreeningMatch(WatchList.Sanctions, 0.82)]);

    [TestMethod]
    public void Valid_document_matching_face_and_clear_screening_is_approved()
    {
        var decision = VerificationDecision.Decide(ValidIdentity, Clear);

        Assert.AreEqual(Outcome.Approved, decision.Outcome);
        Assert.IsEmpty(decision.Reasons);
    }

    [TestMethod]
    [DataRow(DocumentResult.Invalid, true, Reason.DocumentInvalid)]
    [DataRow(DocumentResult.Inconclusive, true, Reason.DocumentInconclusive)]
    [DataRow(DocumentResult.Valid, false, Reason.FaceMismatch)]
    public void An_identity_failure_with_clear_screening_is_rejected(DocumentResult document, bool faceMatch, Reason reason)
    {
        var decision = VerificationDecision.Decide(ValidIdentity with { DocumentResult = document, FaceMatch = faceMatch }, Clear);

        Assert.AreEqual(Outcome.Rejected, decision.Outcome);
        CollectionAssert.AreEqual(new[] { reason }, decision.Reasons.ToArray());
    }

    [TestMethod]
    public void A_possible_sanctions_match_is_referred() =>
        Assert.AreEqual(Outcome.Referred, VerificationDecision.Decide(ValidIdentity, SanctionsMatch).Outcome);

    [TestMethod]
    public void A_possible_PEP_match_is_referred_with_its_own_reason()
    {
        var decision = VerificationDecision.Decide(
            ValidIdentity, new ScreeningCheck("wc_1", ScreeningStatus.PossibleMatch, [new ScreeningMatch(WatchList.Pep, 0.77)]));

        Assert.AreEqual(Outcome.Referred, decision.Outcome);
        CollectionAssert.AreEqual(new[] { Reason.PossiblePepMatch }, decision.Reasons.ToArray());
    }

    // Compliance §3: the review may not be bypassed, so a failed document does not turn a match into a rejection.
    [TestMethod]
    public void A_possible_match_is_referred_even_when_identity_verification_failed()
    {
        var decision = VerificationDecision.Decide(ValidIdentity with { DocumentResult = DocumentResult.Invalid }, SanctionsMatch);

        Assert.AreEqual(Outcome.Referred, decision.Outcome);
        CollectionAssert.AreEquivalent(new[] { Reason.DocumentInvalid, Reason.PossibleSanctionsMatch }, decision.Reasons.ToArray());
    }

    [TestMethod]
    public void A_possible_match_without_details_is_still_referred_as_a_sanctions_match()
    {
        var decision = VerificationDecision.Decide(ValidIdentity, new ScreeningCheck("wc_1", ScreeningStatus.PossibleMatch, []));

        Assert.AreEqual(Outcome.Referred, decision.Outcome);
        CollectionAssert.AreEqual(new[] { Reason.PossibleSanctionsMatch }, decision.Reasons.ToArray());
    }
}
