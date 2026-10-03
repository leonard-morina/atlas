using Asp.Versioning;

namespace Atlas.Onboarding.Api.Extensions;

/// <summary>
/// API versioning and the OpenAPI documents that go with it. Why the API is versioned at all:
/// docs/decisions.md, "API versioning".
/// </summary>
public static class ApiVersioningExtensions
{
    public static IHostApplicationBuilder AddVersionedApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();

        return builder;
    }

    public static WebApplication MapVersionedApiDocs(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithDocumentPerVersion();
        }

        return app;
    }
}
