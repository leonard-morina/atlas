using Atlas.Onboarding.Api.Validation;
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
            new ApplicantIdentifier(request.Identifier!.Type!.Value, request.Identifier.Value!, request.Identifier.IssuingCountry),
            request.Email!,
            request.Phone!,
            [.. request.Documents!.Select(document =>
                new ApplicantDocument(document.Type!.Value, Convert.FromBase64String(document.Image!)))]);
}
