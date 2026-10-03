using FluentValidation;

namespace Atlas.Onboarding.Api.Validation;

/// <summary>
/// Validates a request body before the endpoint runs, so invalid input never reaches business logic.
/// Malformed requests (bad JSON, unknown enum value, missing header) are rejected earlier by model binding
/// with 400; this filter answers 422 for well-formed requests whose content is invalid.
/// </summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(error => ToJsonPath(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    // "Identifier.Value" -> "identifier.value", "Documents[0].Image" -> "documents[0].image": match the JSON.
    private static string ToJsonPath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<TRequest>>();
}
