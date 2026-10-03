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
        var result = await sender.Send(request.ToCommand(idempotencyKey), cancellationToken);

        return TypedResults.Created($"/applications/{result.ApplicationId}", result);
    }
}
