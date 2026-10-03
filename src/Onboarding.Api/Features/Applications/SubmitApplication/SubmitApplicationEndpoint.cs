using Atlas.Onboarding.Api.Validation;
using Atlas.Onboarding.Application.Features.Applications.SubmitApplication;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Onboarding.Api.Features.Applications.SubmitApplication;

/// <summary><c>POST /v{version}/applications</c>: submit an onboarding application.</summary>
public static class SubmitApplicationEndpoint
{
    public static RouteGroupBuilder MapSubmitApplication(this RouteGroupBuilder applications)
    {
        applications.MapPost("", HandleV1)
            .MapToApiVersion(1)
            .WithName("SubmitApplicationV1")
            .WithValidation<SubmitApplicationRequest>()
            .Produces<SubmitApplicationResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return applications;
    }

    /// <param name="idempotencyKey">
    /// A UUID the app generates once per application and resends on every retry, so a repeated submission
    /// is recognised instead of creating a second application.
    /// </param>
    /// <param name="request">The application.</param>
    /// <param name="sender">MediatR.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    private static async Task<IResult> HandleV1(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        SubmitApplicationRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(ToCommand(idempotencyKey, request), cancellationToken);

        // The public path: mobile reaches this service through the gateway's unversioned routes.
        return TypedResults.Created($"/applications/{result.ApplicationId}", result);
    }

    // The validation filter has already run, so the required values are present and well-formed.
    private static SubmitApplicationCommand ToCommand(Guid idempotencyKey, SubmitApplicationRequest request) =>
        new(
            idempotencyKey,
            request.FirstName!,
            request.LastName!,
            request.DateOfBirth!.Value,
            Market: request.Country!,
            request.Nationality!,
            new ApplicantIdentifier(
                ToDomain(request.Identifier!.Type!.Value),
                request.Identifier.Value!,
                request.Identifier.IssuingCountry),
            request.Email!,
            request.Phone!,
            [.. request.Documents!.Select(document =>
                new ApplicantDocument(ToDomain(document.Type!.Value), Convert.FromBase64String(document.Image!)))]);

    // The v1 contract keeps its own enums so the JSON shape and the domain can change independently.
    private static Domain.Applications.IdentifierType ToDomain(IdentifierType type) => type switch
    {
        IdentifierType.NationalId => Domain.Applications.IdentifierType.NationalId,
        IdentifierType.Passport => Domain.Applications.IdentifierType.Passport,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static Domain.Applications.DocumentType ToDomain(DocumentType type) => type switch
    {
        DocumentType.Passport => Domain.Applications.DocumentType.Passport,
        DocumentType.IdCard => Domain.Applications.DocumentType.IdCard,
        DocumentType.Selfie => Domain.Applications.DocumentType.Selfie,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
