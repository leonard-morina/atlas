using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Onboarding.Api.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Metadata;

namespace Atlas.Onboarding.Api.Validation;

/// <summary>
/// A body that cannot be deserialized (unknown enum value, malformed date, wrong JSON type) is reported against
/// the field it failed on, in the same "errors" shape as a 422, instead of a generic "failed to read the body".
/// </summary>
public static partial class UnreadableBodyErrors
{
    public static void AddTo(ProblemDetailsContext context)
    {
        if (context.Exception is not BadHttpRequestException { InnerException: JsonException { Path: { } path } })
        {
            return;
        }

        // The exception handler clears the current endpoint; the one that failed is kept on its feature.
        var endpoint = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        var bodyType = endpoint?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;

        context.ProblemDetails.Title = "The request body could not be read.";

        if (path == "$")
        {
            context.ProblemDetails.Detail = "The request body is not valid JSON.";
            return;
        }

        var field = path.TrimStart('$', '.');
        context.ProblemDetails.Detail = $"The value of '{field}' could not be read.";
        context.ProblemDetails.Extensions["errors"] = new Dictionary<string, string[]>
        {
            [field] = [Describe(bodyType, path)],
        };
    }

    /// <summary>What the field at a JSON path (e.g. <c>$.documents[0].type</c>) accepts.</summary>
    public static string Describe(Type? bodyType, string jsonPath) => TypeAt(bodyType, jsonPath) switch
    {
        { IsEnum: true } type =>
            $"Must be one of: {string.Join(", ", Enum.GetNames(type).Select(HttpApiExtensions.EnumNaming.ConvertName))}.",
        { } type when type == typeof(DateOnly) => "Must be a date in the form YYYY-MM-DD, e.g. 1991-03-04.",
        { } type when type == typeof(bool) => "Must be true or false.",
        _ => "Not a valid value for this field.",
    };

    // Follows the path through the request type: property names are camelCase in JSON, [n] is a list element.
    private static Type? TypeAt(Type? type, string jsonPath)
    {
        foreach (Match segment in PathSegment().Matches(jsonPath))
        {
            type = segment.Groups["index"].Success
                ? ElementType(type)
                : type?.GetProperties().FirstOrDefault(property =>
                    JsonNamingPolicy.CamelCase.ConvertName(property.Name) == segment.Groups["name"].Value)?.PropertyType;
        }

        return type is null ? null : Nullable.GetUnderlyingType(type) ?? type;
    }

    private static Type? ElementType(Type? type) =>
        type?.GetInterfaces().Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];

    [GeneratedRegex(@"\.(?<name>\w+)|\[(?<index>\d+)\]")]
    private static partial Regex PathSegment();
}
