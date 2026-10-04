using Atlas.Onboarding.Api.Features.Applications.GetApplication;
using Atlas.Onboarding.Api.Features.Applications.SubmitApplication;
using FluentValidation;

namespace Atlas.Onboarding.Api.Features.Applications;

/// <summary>
/// The versioned applications API. Versions are declared here once; each endpoint states which
/// versions it serves, so a new version only needs new code for the endpoints that change.
/// </summary>
/// <remarks>
/// Deliberately ahead of need: with a single version this service would not normally be versioned yet.
/// It is here to show how the contract evolves without breaking installed apps. Mobile only ever calls
/// the unversioned <c>POST /applications</c>; the gateway maps that to <c>/v1/applications</c> here.
/// See docs/decisions.md, "API versioning".
/// </remarks>
public static class ApplicationsApi
{
    public static IHostApplicationBuilder AddApplicationsApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssemblyContaining<SubmitApplicationRequestValidator>();
        builder.Services.Configure<DecisionWaitOptions>(builder.Configuration.GetSection("Onboarding:DecisionWait"));
        builder.Services.AddScoped<DecisionWait>();

        return builder;
    }

    public static IEndpointRouteBuilder MapApplicationsApi(this IEndpointRouteBuilder endpoints)
    {
        var applications = endpoints.NewVersionedApi("Applications")
            .MapGroup("/v{version:apiVersion}/applications")
            .HasApiVersion(1);

        applications.MapSubmitApplication();
        applications.MapGetApplication();

        return endpoints;
    }
}
