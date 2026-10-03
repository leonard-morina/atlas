using System.Diagnostics;
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
            .Produces<SubmitApplicationResponse>(StatusCodes.Status201Created)
            .Produces<SubmitApplicationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return applications;
    }

    /// <param name="idempotencyKey">
    /// A UUID the app generates once per application and resends on every retry, so a repeated submission
    /// is recognised instead of creating a second application.
    /// </param>
    /// <param name="request">The application.</param>
    /// <param name="sender">MediatR.</param>
    /// <param name="httpContext">The current request.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    private static async Task<IResult> HandleV1(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        SubmitApplicationRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        await sender.Send(request.ToCommand(idempotencyKey), cancellationToken) switch
        {
            SubmitApplicationResult.Accepted accepted => Accepted(accepted, httpContext),

            SubmitApplicationResult.IdempotencyKeyReused => TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency-Key already used.",
                detail: "This Idempotency-Key was used for a different application. Use a new key for a new application."),

            SubmitApplicationResult.ApplicantAlreadyApplied => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Application already in progress.",
                detail: "There is already an application for this identifier in this market."),

            _ => throw new UnreachableException(),
        };

    // 201 with a decision, 202 while the decision is pending. A retry gets the original answer, marked as such.
    private static IResult Accepted(SubmitApplicationResult.Accepted accepted, HttpContext httpContext)
    {
        if (accepted.Replayed)
        {
            httpContext.Response.Headers["Idempotent-Replayed"] = "true";
        }

        var response = new SubmitApplicationResponse(accepted.ApplicationId, accepted.Status.ToContract());

        // The public path: mobile reaches this service through the gateway's unversioned routes.
        var location = $"/applications/{accepted.ApplicationId}";

        return response.Status.IsFinal()
            ? TypedResults.Created(location, response)
            : TypedResults.Accepted(location, response);
    }
}
