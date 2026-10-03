using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Atlas.Onboarding.Api.OpenApi;

/// <summary>
/// Request fields are nullable in C# only so that a missing field is reported per field (422) instead of failing
/// deserialization. In the published contract a required field is never null, so this removes the null from
/// required properties and from enums. Optional properties keep theirs.
/// </summary>
public sealed class RequiredPropertiesAreNotNullTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var schema in (document.Components?.Schemas?.Values ?? []).OfType<OpenApiSchema>())
        {
            RemoveNullFromEnum(schema);

            if (schema is not { Properties: { } properties, Required: { } required })
            {
                continue;
            }

            foreach (var name in required.Where(properties.ContainsKey))
            {
                properties[name] = WithoutNull(properties[name]);
            }
        }

        return Task.CompletedTask;
    }

    // An enum lists null when it is used through a nullable property; that property says so itself.
    private static void RemoveNullFromEnum(OpenApiSchema schema)
    {
        for (var i = (schema.Enum?.Count ?? 0) - 1; i >= 0; i--)
        {
            if (schema.Enum![i] is null)
            {
                schema.Enum.RemoveAt(i);
            }
        }
    }

    private static IOpenApiSchema WithoutNull(IOpenApiSchema property)
    {
        if (property is not OpenApiSchema schema)
        {
            return property;
        }

        // A nullable reference is written as oneOf [null, $ref]; required means just the $ref.
        if (schema.OneOf is { Count: 2 } oneOf && oneOf.Any(IsNull))
        {
            return oneOf.First(alternative => !IsNull(alternative));
        }

        if (schema.Type is { } type)
        {
            schema.Type = type & ~JsonSchemaType.Null;
        }

        return schema;
    }

    private static bool IsNull(IOpenApiSchema schema) => schema.Type == JsonSchemaType.Null;
}
