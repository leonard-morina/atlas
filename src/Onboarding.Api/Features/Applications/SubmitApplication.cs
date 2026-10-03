namespace Atlas.Onboarding.Api.Features.Applications;

/// <summary>
/// <c>POST /v{version}/applications</c> — submit an onboarding application.
/// </summary>
public static class SubmitApplication
{
    public static RouteGroupBuilder MapSubmitApplication(this RouteGroupBuilder applications)
    {
        applications.MapPost("", HandleV1)
            .MapToApiVersion(1)
            .WithName("SubmitApplicationV1");

        return applications;
    }

    private static IResult HandleV1() =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
}
