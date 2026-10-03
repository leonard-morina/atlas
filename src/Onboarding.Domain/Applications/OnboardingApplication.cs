using Atlas.Onboarding.Domain.Markets;

namespace Atlas.Onboarding.Domain.Applications;

/// <summary>
/// A customer's onboarding application, the aggregate root. It has its own identity and is never keyed
/// by the national identifier (Annex B, note 2).
/// </summary>
public sealed class OnboardingApplication
{
    private OnboardingApplication(
        Guid id,
        Guid idempotencyKey,
        string requestFingerprint,
        string market,
        Applicant applicant,
        ApplicantIdentifier identifier,
        DateTimeOffset submittedAt)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        Market = market;
        Applicant = applicant;
        Identifier = identifier;
        Status = ApplicationStatus.Submitted;
        SubmittedAt = submittedAt;
    }

    // Used by persistence to rebuild a stored application; application code goes through Submit.
    private OnboardingApplication()
    {
        RequestFingerprint = null!;
        Market = null!;
        Applicant = null!;
        Identifier = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Sent by the app with every retry: a repeated submission is the same application.</summary>
    public Guid IdempotencyKey { get; private set; }

    /// <summary>
    /// A hash of what was submitted. A retry carries the same key and the same content; the same key with
    /// different content is a client error, not a retry.
    /// </summary>
    public string RequestFingerprint { get; private set; }

    public string Market { get; private set; }

    public Applicant Applicant { get; private set; }

    public ApplicantIdentifier Identifier { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// The only way an application comes into existence. The market rules are enforced here as well as at
    /// the HTTP edge, so no entry point can create an application that breaks them.
    /// </summary>
    /// <exception cref="ArgumentException">The identifier is not valid for the market.</exception>
    public static OnboardingApplication Submit(
        Guid idempotencyKey,
        string requestFingerprint,
        Market market,
        Applicant applicant,
        ApplicantIdentifier identifier,
        DateTimeOffset submittedAt)
    {
        if (IdentifierProblem(market, applicant, identifier) is { } problem)
        {
            throw new ArgumentException(problem, nameof(identifier));
        }

        return new OnboardingApplication(
            Guid.NewGuid(), idempotencyKey, requestFingerprint, market.Code, applicant, identifier, submittedAt);
    }

    /// <summary>
    /// A rejected applicant may apply again; any other application in a market blocks a second one for the
    /// same identifier.
    /// </summary>
    public bool BlocksNewApplication => Status != ApplicationStatus.Rejected;

    private static string? IdentifierProblem(Market market, Applicant applicant, ApplicantIdentifier identifier) =>
        identifier.Type switch
        {
            IdentifierType.NationalId => market.NationalId.Validate(identifier.Value, applicant.DateOfBirth),
            IdentifierType.Passport when !market.AcceptsPassport => $"Passports are not accepted in market {market.Code}.",
            IdentifierType.Passport when string.IsNullOrEmpty(identifier.IssuingCountry) => "A passport needs its issuing country.",
            IdentifierType.Passport => null,
            _ => $"Unknown identifier type {identifier.Type}.",
        };
}
