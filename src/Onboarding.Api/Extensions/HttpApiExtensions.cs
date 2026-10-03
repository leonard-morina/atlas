using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Onboarding.Api.Extensions;

/// <summary>Conventions every HTTP response follows: problem details for errors, enums as strings.</summary>
public static class HttpApiExtensions
{
    public static IHostApplicationBuilder AddHttpApiConventions(this IHostApplicationBuilder builder)
    {
        // Errors are RFC 9457 problem details. A binding failure (malformed JSON, unknown enum value, missing
        // header) keeps its message: it only describes the request, so it is safe and useful to return.
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (context.Exception is BadHttpRequestException or NotImplementedException)
            {
                context.ProblemDetails.Detail = context.Exception.Message;
            }
        });

        // Enums travel as upper snake case, as in the ticket's draft ("NATIONAL_ID", "ID_CARD"); numbers are refused.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false)));

        return builder;
    }

    public static WebApplication UseHttpApiConventions(this WebApplication app)
    {
        // Binding failures are thrown as BadHttpRequestException; answer with their status (400), not 500.
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = exception => exception switch
            {
                BadHttpRequestException badRequest => badRequest.StatusCode,
                NotImplementedException => StatusCodes.Status501NotImplemented,
                _ => StatusCodes.Status500InternalServerError,
            },
        });
        app.UseStatusCodePages();

        return app;
    }
}
