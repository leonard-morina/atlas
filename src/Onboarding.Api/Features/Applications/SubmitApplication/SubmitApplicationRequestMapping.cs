using Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary>
/// Maps the v1 contract to the command. The contract keeps its own enums so the JSON shape and the
/// domain can change independently; a future v2 request gets its own mapping to the same command.
/// </summary>
public static class SubmitApplicationRequestMapping
{
    // Since the validation filter has already passed, we no longer need to validate the request body
    public static SubmitApplicationCommand ToCommand(this SubmitApplicationRequest request, Guid idempotencyKey) =>
        new(
            idempotencyKey,
            request.FirstName!,
            request.LastName!,
            request.DateOfBirth!.Value,
            Market: request.Country!,
            request.Nationality!,
            new Domain.Applications.ApplicantIdentifier(
                request.Identifier!.Type!.Value.ToDomain(),
                request.Identifier.Value!,
                request.Identifier.IssuingCountry),
            request.Email!,
            request.Phone!,
            [
                .. request.Documents!.Select(document =>
                    new ApplicantDocument(document.Type!.Value.ToDomain(), Convert.FromBase64String(document.Image!)))
            ]);

    public static Domain.Applications.IdentifierType ToDomain(this IdentifierType type) => type switch
    {
        IdentifierType.NationalId => Domain.Applications.IdentifierType.NationalId,
        IdentifierType.Passport => Domain.Applications.IdentifierType.Passport,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static Domain.Applications.DocumentType ToDomain(this DocumentType type) => type switch
    {
        DocumentType.Passport => Domain.Applications.DocumentType.Passport,
        DocumentType.IdCard => Domain.Applications.DocumentType.IdCard,
        DocumentType.Selfie => Domain.Applications.DocumentType.Selfie,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
