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

    [TestMethod]
    public void Submit_creates_a_submitted_application_with_its_own_identity()
    {
        var key = Guid.NewGuid();

        var application = OnboardingApplication.Submit(key, MB, Ana, ValidPersonalNumber, Now);

        Assert.AreNotEqual(Guid.Empty, application.Id);
        Assert.AreNotEqual(key, application.Id);
        Assert.AreEqual(key, application.IdempotencyKey);
        Assert.AreEqual("MB", application.Market);
        Assert.AreEqual(ApplicationStatus.Submitted, application.Status);
        Assert.AreEqual(Now, application.SubmittedAt);
    }

    [TestMethod]
    public void Submit_refuses_a_national_id_that_is_invalid_for_the_market() =>
        Assert.ThrowsExactly<ArgumentException>(() => OnboardingApplication.Submit(
            Guid.NewGuid(), MB, Ana, new ApplicantIdentifier(IdentifierType.NationalId, "0403991450016", null), Now));

    [TestMethod]
    public void Submit_refuses_a_passport_where_the_market_does_not_accept_one() =>
        Assert.ThrowsExactly<ArgumentException>(() => OnboardingApplication.Submit(
            Guid.NewGuid(), MB, Ana, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", "SYR"), Now));

    [TestMethod]
    public void Submit_refuses_a_passport_without_its_issuing_country() =>
        Assert.ThrowsExactly<ArgumentException>(() => OnboardingApplication.Submit(
            Guid.NewGuid(), MF, Ana, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", null), Now));

    [TestMethod]
    public void Submit_accepts_a_passport_in_MF() =>
        Assert.AreEqual(
            ApplicationStatus.Submitted,
            OnboardingApplication.Submit(Guid.NewGuid(), MF, Ana, new ApplicantIdentifier(IdentifierType.Passport, "N1234567", "SYR"), Now).Status);
}
