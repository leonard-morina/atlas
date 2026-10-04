using Atlas.Onboarding.Domain.Applications;
using Atlas.Onboarding.Domain.Markets;
using Atlas.Onboarding.Domain.Markets.Identifiers;

namespace Atlas.Onboarding.UnitTests.Applications;

[TestClass]
public sealed class OnboardingApplicationTests
{
    private static readonly Market MB = new("MB", AcceptsPassport: false, ActivationMode.Remote, new PersonalNumberValidator());
    private static readonly Market MF = new("MF", AcceptsPassport: true, ActivationMode.Remote, new PersonalNumberValidator());

    private static readonly Applicant Ana = new("Ana", "Petrova", new DateOnly(1991, 3, 4), "MKD", "ana@example.com", "+38970123456");
    private static readonly ApplicantIdentifier ValidPersonalNumber = new(IdentifierType.NationalId, "0403991450014", null);
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static readonly ApplicationDocument Passport = new(DocumentType.Passport, "MB/x/passport", "AB", 10);
    private static readonly ApplicationDocument Selfie = new(DocumentType.Selfie, "MB/x/selfie", "CD", 10);
    private static readonly ApplicationDocument[] Documents = [Passport, Selfie];

    [TestMethod]
    public void Submit_creates_a_submitted_application()
    {
        var id = Guid.NewGuid();
        var key = Guid.NewGuid();

        var application = OnboardingApplication.Submit(id, key, "fingerprint", MB, Ana, ValidPersonalNumber, Documents, Now);

        Assert.AreEqual(id, application.Id);
        Assert.AreEqual(key, application.IdempotencyKey);
        Assert.AreEqual("MB", application.Market);
        Assert.AreEqual(ApplicationStatus.Submitted, application.Status);
        Assert.AreEqual(Now, application.SubmittedAt);
        CollectionAssert.AreEqual(Documents, application.Documents.ToArray());
    }

    [TestMethod]
    public void Submit_refuses_a_national_id_that_is_invalid_for_the_market() =>
        Assert.ThrowsExactly<ArgumentException>(() => Submit(MB, new ApplicantIdentifier(IdentifierType.NationalId, "0403991450016", null)));

    [TestMethod]
    public void Submit_refuses_a_passport_where_the_market_does_not_accept_one() =>
        Assert.ThrowsExactly<ArgumentException>(() => Submit(MB, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", "SYR")));

    [TestMethod]
    public void Submit_refuses_a_passport_without_its_issuing_country() =>
        Assert.ThrowsExactly<ArgumentException>(() => Submit(MF, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", null)));

    [TestMethod]
    public void Submit_accepts_a_passport_in_MF() =>
        Assert.AreEqual(
            ApplicationStatus.Submitted,
            Submit(MF, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", "SYR")).Status);

    [TestMethod]
    [DataRow(0, DisplayName = "no documents")]
    [DataRow(1, DisplayName = "selfie only")]
    [DataRow(2, DisplayName = "two selfies")]
    public void Submit_refuses_documents_other_than_one_identity_document_and_one_selfie(int variant)
    {
        ApplicationDocument[] documents = variant switch
        {
            0 => [],
            1 => [Selfie],
            _ => [Selfie, Selfie with { BlobName = "MB/x/selfie2" }],
        };

        Assert.ThrowsExactly<ArgumentException>(() =>
            OnboardingApplication.Submit(Guid.NewGuid(), Guid.NewGuid(), "fingerprint", MB, Ana, ValidPersonalNumber, documents, Now));
    }

    [TestMethod]
    [DataRow(VerificationVerdict.Approved, ApplicationStatus.Approved)]
    [DataRow(VerificationVerdict.Rejected, ApplicationStatus.Rejected)]
    [DataRow(VerificationVerdict.Referred, ApplicationStatus.Referred)]
    public void RecordVerification_moves_a_submitted_application_to_the_verdict(VerificationVerdict verdict, ApplicationStatus expected)
    {
        var application = Submit(MB, ValidPersonalNumber);

        application.RecordVerification(verdict, MB, Now.AddSeconds(2));

        Assert.AreEqual(expected, application.Status);
        Assert.AreEqual(Now.AddSeconds(2), application.DecidedAt);
    }

    // Annex B: in MD remote identification covers the application only; activation needs a branch visit.
    [TestMethod]
    public void RecordVerification_approval_in_a_branch_activation_market_awaits_the_branch_visit()
    {
        var md = new Market("MD", AcceptsPassport: false, ActivationMode.InBranch, new PersonalNumberValidator());
        var application = Submit(md, ValidPersonalNumber);

        application.RecordVerification(VerificationVerdict.Approved, md, Now);

        Assert.AreEqual(ApplicationStatus.AwaitingBranchVisit, application.Status);
    }

    [TestMethod]
    public void RecordVerification_of_the_same_verdict_again_changes_nothing()
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(VerificationVerdict.Approved, MB, Now);

        application.RecordVerification(VerificationVerdict.Approved, MB, Now.AddMinutes(5));

        Assert.AreEqual(ApplicationStatus.Approved, application.Status);
        Assert.AreEqual(Now, application.DecidedAt);
    }

    [TestMethod]
    public void RecordVerification_refuses_a_different_verdict_once_decided()
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(VerificationVerdict.Referred, MB, Now);

        Assert.ThrowsExactly<InvalidOperationException>(() => application.RecordVerification(VerificationVerdict.Approved, MB, Now));
    }

    [TestMethod]
    public void RecordAccountOpened_moves_an_approved_application_on()
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(VerificationVerdict.Approved, MB, Now);

        application.RecordAccountOpened("MB0000000001", Now.AddMinutes(2));

        Assert.AreEqual(ApplicationStatus.AccountOpened, application.Status);
        Assert.AreEqual("MB0000000001", application.AccountNumber);
        Assert.AreEqual(Now.AddMinutes(2), application.AccountOpenedAt);
    }

    [TestMethod]
    public void RecordAccountOpened_of_the_same_account_again_changes_nothing_and_a_second_is_refused()
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(VerificationVerdict.Approved, MB, Now);
        application.RecordAccountOpened("MB0000000001", Now);

        application.RecordAccountOpened("MB0000000001", Now.AddMinutes(1));

        Assert.AreEqual(Now, application.AccountOpenedAt);
        Assert.ThrowsExactly<InvalidOperationException>(() => application.RecordAccountOpened("MB0000000002", Now));
    }

    [TestMethod]
    [DataRow(VerificationVerdict.Referred)]
    [DataRow(VerificationVerdict.Rejected)]
    public void RecordAccountOpened_is_refused_without_an_approval(VerificationVerdict verdict)
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(verdict, MB, Now);

        Assert.ThrowsExactly<InvalidOperationException>(() => application.RecordAccountOpened("MB0000000001", Now));
    }

    [TestMethod]
    public void RecordVerification_of_the_approval_again_after_the_account_opened_changes_nothing()
    {
        var application = Submit(MB, ValidPersonalNumber);
        application.RecordVerification(VerificationVerdict.Approved, MB, Now);
        application.RecordAccountOpened("MB0000000001", Now);

        application.RecordVerification(VerificationVerdict.Approved, MB, Now.AddMinutes(5));

        Assert.AreEqual(ApplicationStatus.AccountOpened, application.Status);
        Assert.IsFalse(application.NeedsAccount);
    }

    [TestMethod]
    public void Only_an_approval_with_remote_activation_needs_an_account()
    {
        var md = new Market("MD", AcceptsPassport: false, ActivationMode.InBranch, new PersonalNumberValidator());
        var inBranch = Submit(md, ValidPersonalNumber);
        var remote = Submit(MB, ValidPersonalNumber);

        inBranch.RecordVerification(VerificationVerdict.Approved, md, Now);
        remote.RecordVerification(VerificationVerdict.Approved, MB, Now);

        Assert.IsFalse(inBranch.NeedsAccount);
        Assert.IsTrue(remote.NeedsAccount);
    }

    private static OnboardingApplication Submit(Market market, ApplicantIdentifier identifier) =>
        OnboardingApplication.Submit(Guid.NewGuid(), Guid.NewGuid(), "fingerprint", market, Ana, identifier, Documents, Now);
}
