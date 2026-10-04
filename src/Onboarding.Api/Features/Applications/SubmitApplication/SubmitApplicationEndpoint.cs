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
    /// <param name="scopes">Creates the submission's own unit of work.</param>
    /// <param name="decisionWait">Waits briefly for verification to decide.</param>
    /// <param name="httpContext">The current request.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    private static async Task<IResult> HandleV1(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        SubmitApplicationRequest request,
        IServiceScopeFactory scopes,
        DecisionWait decisionWait,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        await SubmitAsync(scopes, request.ToCommand(idempotencyKey), cancellationToken) switch
        {
            SubmitApplicationResult.Accepted accepted =>
                await AcceptedAsync(accepted, decisionWait, httpContext, cancellationToken),

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

    // The submission is its own unit of work, ended before the wait for a decision begins. The outbox hands
    // ApplicationSubmitted to the broker when that unit of work is disposed; waiting inside it (the request's scope)
    // would hold back the very message verification needs, for the whole wait.
    private static async Task<SubmitApplicationResult> SubmitAsync(
        IServiceScopeFactory scopes,
        SubmitApplicationCommand command,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    // 201 with a decision, 202 while the decision is pending. A retry gets the original answer, marked as such.
    private static async Task<IResult> AcceptedAsync(
        SubmitApplicationResult.Accepted accepted,
        DecisionWait decisionWait,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (accepted.Replayed)
        {
            httpContext.Response.Headers["Idempotent-Replayed"] = "true";
        }

        // Usually verification decides within the wait; when it does not, the app checks back later.
        var state = await decisionWait.ForDecisionAsync(accepted.ApplicationId, cancellationToken);
        var response = new SubmitApplicationResponse(accepted.ApplicationId, (state?.Status ?? accepted.Status).ToContract());

        // The public path: mobile reaches this service through the gateway's unversioned routes.
        var location = $"/applications/{accepted.ApplicationId}";

        return response.Status.IsFinal()
            ? TypedResults.Created(location, response)
            : TypedResults.Accepted(location, response);
    }
}
