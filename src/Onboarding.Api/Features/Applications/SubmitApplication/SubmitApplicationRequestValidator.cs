using System.Buffers.Text;
using System.Text.RegularExpressions;
using Atlas.Onboarding.Domain.Markets;
using FluentValidation;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>
/// Customer input rules, checked before any provider is called. A syntactically invalid identifier is a
/// customer input error, not a verification failure (CDS-ID-04 §1.2), so it is a 422, never a rejection.
/// </summary>
public sealed partial class SubmitApplicationRequestValidator : AbstractValidator<SubmitApplicationRequest>
{
    /// <summary>Per decoded image. Teams (29 July): images are "several MB each".</summary>
    public const int MaxImageBytes = 10 * 1024 * 1024;

    private readonly SupportedMarkets _supportedMarkets;

    public SubmitApplicationRequestValidator(SupportedMarkets supportedMarkets, TimeProvider time)
    {
        _supportedMarkets = supportedMarkets;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.LastName).NotEmpty().MaximumLength(100);

        RuleFor(r => r.DateOfBirth)
            .NotNull()
            .Must(date => date!.Value.Year >= 1900 && date.Value < DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
            .WithMessage("Must be a date in the past.");

        RuleFor(r => r.Country)
            .NotEmpty()
            .Must(code => supportedMarkets.TryGet(code, out _))
            .WithMessage("'{PropertyValue}' is not a supported market.");

        RuleFor(r => r.Nationality)
            .NotEmpty()
            .Matches(CountryCode())
            .WithMessage("Must be an ISO 3166-1 alpha-3 country code, e.g. 'MKD'.");

        RuleFor(r => r.Identifier).NotNull().Custom(ValidateIdentifier);

        RuleFor(r => r.Email).NotEmpty().EmailAddress();

        RuleFor(r => r.Phone)
            .NotEmpty()
            .Matches(InternationalPhone())
            .WithMessage("Must be in international format, e.g. '+38970123456'.");

        RuleFor(r => r.Documents)
            .NotEmpty()
            .Must(HaveOneIdentityDocumentAndOneSelfie)
            .WithMessage("Exactly one identity document (PASSPORT or ID_CARD) and one SELFIE are required.");

        RuleForEach(r => r.Documents).ChildRules(document =>
        {
            document.RuleFor(d => d.Type).NotNull();
            document.RuleFor(d => d.Image)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(image => Base64.IsValid(image, out _)).WithMessage("Must be base64-encoded.")
                .Must(image => Base64.IsValid(image, out var bytes) && bytes <= MaxImageBytes)
                .WithMessage($"Must be at most {MaxImageBytes / (1024 * 1024)} MB.");
        });

        RuleFor(r => r.TermsAccepted).Equal(true).WithMessage("Terms must be accepted.");
    }

    // The market decides which identifiers are valid; an unknown market is reported on Country instead.
    private void ValidateIdentifier(IdentifierRequest? identifier, ValidationContext<SubmitApplicationRequest> context)
    {
        var request = context.InstanceToValidate;

        if (identifier!.Type is not { } type)
        {
            context.AddFailure("Identifier.Type", "Required.");
            return;
        }

        if (string.IsNullOrEmpty(identifier.Value))
        {
            context.AddFailure("Identifier.Value", "Required.");
            return;
        }

        if (!_supportedMarkets.TryGet(request.Country, out var market))
        {
            return;
        }

        switch (type)
        {
            case IdentifierType.NationalId:
                if (!string.IsNullOrEmpty(identifier.IssuingCountry))
                {
                    context.AddFailure("Identifier.IssuingCountry", "Only used for passports.");
                }

                if (request.DateOfBirth is { } dateOfBirth
                    && market.NationalId.Validate(identifier.Value, dateOfBirth) is { } error)
                {
                    context.AddFailure("Identifier.Value", error);
                }

                break;

            case IdentifierType.Passport:
                if (!market.AcceptsPassport)
                {
                    context.AddFailure("Identifier.Type", $"Passports are not accepted as identifier in market {market.Code}.");
                    return;
                }

                // CDS-ID-04 §2: a passport number alone does not identify a person; the issuing state is required.
                if (identifier.IssuingCountry is null || !CountryCode().IsMatch(identifier.IssuingCountry))
                {
                    context.AddFailure("Identifier.IssuingCountry", "Must be an ISO 3166-1 alpha-3 country code, e.g. 'SYR'.");
                }

                if (!PassportNumber().IsMatch(identifier.Value))
                {
                    context.AddFailure("Identifier.Value", "Must be up to 9 characters, A-Z and 0-9.");
                }

                break;
        }
    }

    private static bool HaveOneIdentityDocumentAndOneSelfie(IReadOnlyList<DocumentRequest>? documents) =>
        documents!.Count == 2
        && documents.Count(d => d.Type == DocumentType.Selfie) == 1
        && documents.Count(d => d.Type is DocumentType.Passport or DocumentType.IdCard) == 1;

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CountryCode();

    // ICAO 9303.
    [GeneratedRegex("^[A-Z0-9]{1,9}$")]
    private static partial Regex PassportNumber();

    // E.164.
    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$")]
    private static partial Regex InternationalPhone();
}
