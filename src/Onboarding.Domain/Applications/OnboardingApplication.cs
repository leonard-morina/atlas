using Atlas.Onboarding.Domain.Markets;

namespace Atlas.Onboarding.Domain.Applications;

/// <summary>
/// A customer's onboarding application, the aggregate root. It has its own identity and is never keyed
/// by the national identifier (Annex B, note 2).
/// </summary>
public sealed class OnboardingApplication
{
    private readonly List<ApplicationDocument> _documents = [];

    private OnboardingApplication(
        Guid id,
        Guid idempotencyKey,
        string requestFingerprint,
        string market,
        Applicant applicant,
        ApplicantIdentifier identifier,
        IEnumerable<ApplicationDocument> documents,
        DateTimeOffset submittedAt)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        Market = market;
        Applicant = applicant;
        Identifier = identifier;
        _documents.AddRange(documents);
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

    /// <summary>Exactly one identity document (passport or ID card) and one selfie.</summary>
    public IReadOnlyList<ApplicationDocument> Documents => _documents;

    public ApplicationStatus Status { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// The only way an application comes into existence. The market rules are enforced here as well as at
    /// the HTTP edge, so no entry point can create an application that breaks them.
    /// </summary>
    /// <param name="id">
    /// Chosen by the caller because the documents are stored under it before the application itself is saved.
    /// </param>
    /// <exception cref="ArgumentException">The identifier is not valid for the market, or the documents are incomplete.</exception>
    public static OnboardingApplication Submit(
        Guid id,
        Guid idempotencyKey,
        string requestFingerprint,
        Market market,
        Applicant applicant,
        ApplicantIdentifier identifier,
        IReadOnlyCollection<ApplicationDocument> documents,
        DateTimeOffset submittedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);

        if (IdentifierProblem(market, applicant, identifier) is { } problem)
        {
            throw new ArgumentException(problem, nameof(identifier));
        }

        if (!HasOneIdentityDocumentAndOneSelfie(documents))
        {
            throw new ArgumentException("Exactly one identity document and one selfie are required.", nameof(documents));
        }

        return new OnboardingApplication(
            id, idempotencyKey, requestFingerprint, market.Code, applicant, identifier, documents, submittedAt);
    }

    /// <summary>
    /// A rejected applicant may apply again; any other application in a market blocks a second one for the
    /// same identifier.
    /// </summary>
    public bool BlocksNewApplication => Status != ApplicationStatus.Rejected;

    private static bool HasOneIdentityDocumentAndOneSelfie(IReadOnlyCollection<ApplicationDocument> documents) =>
        documents.Count == 2
        && documents.Count(document => document.Type == DocumentType.Selfie) == 1
        && documents.Count(document => document.Type is DocumentType.Passport or DocumentType.IdCard) == 1;

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
