using Atlas.Onboarding.Application.Features.Applications.GetApplication;
using MediatR;

namespace Atlas.Onboarding.Api.Features.Applications.GetApplication;

/// <summary><c>GET /v{version}/applications/{applicationId}</c>: where an application stands.</summary>
public static class GetApplicationEndpoint
{
    public static RouteGroupBuilder MapGetApplication(this RouteGroupBuilder applications)
    {
        applications.MapGet("{applicationId:guid}", HandleV1)
            .MapToApiVersion(1)
            .WithName("GetApplicationV1")
            .Produces<ApplicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return applications;
    }

    /// <param name="applicationId">As returned when the application was submitted.</param>
    /// <param name="sender">MediatR.</param>
    /// <param name="cancellationToken">Request aborted.</param>
    private static async Task<IResult> HandleV1(Guid applicationId, ISender sender, CancellationToken cancellationToken) =>
        await sender.Send(new GetApplicationQuery(applicationId), cancellationToken) is { } application
            ? TypedResults.Ok(application.ToResponse())
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Application not found.");

    private static ApplicationResponse ToResponse(this ApplicationState application) =>
        new(application.ApplicationId, application.Status.ToContract(), application.SubmittedAt, application.DecidedAt);
}
