using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Stubs.Calls;
using Microsoft.Extensions.Options;

namespace Atlas.Stubs.IdNow;

/// <param name="DocumentType"><c>PASSPORT</c> or <c>ID_CARD</c>.</param>
public sealed record IdentificationRequest(string? DocumentType, string? DocumentImage, string? SelfieImage);

/// <param name="DocumentResult"><c>VALID</c>, <c>INVALID</c> or <c>INCONCLUSIVE</c>.</param>
public sealed record IdentificationResponse(string IdentificationId, string DocumentResult, bool FaceMatch, double Confidence);

/// <summary>
/// Stands in for IDNow's <c>POST /v1/identifications</c> (shape as in ATLAS-1). IDNow receives no name, so the
/// scenario is chosen by a marker inside the document image: <c>IDNOW:INVALID</c>, <c>IDNOW:INCONCLUSIVE</c>,
/// <c>IDNOW:NOFACE</c>, <c>IDNOW:SLOW</c> or <c>IDNOW:DOWN</c>. Any other image is a valid document and a face match.
/// </summary>
public static partial class IdNowEndpoints
{
    public const string Provider = "idnow";

    public static IEndpointRouteBuilder MapIdNow(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/idnow/v1/identifications", IdentifyAsync);
        return endpoints;
    }

    private static async Task<IResult> IdentifyAsync(
        IdentificationRequest request,
        CallLog calls,
        IOptions<StubOptions> options,
        CancellationToken cancellationToken)
    {
        if (request.DocumentType is not ("PASSPORT" or "ID_CARD")
            || !IsBase64(request.DocumentImage)
            || !IsBase64(request.SelfieImage))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid identification request.");
        }

        var scenario = Marker().Match(Encoding.ASCII.GetString(Convert.FromBase64String(request.DocumentImage!)))
            is { Success: true } match ? match.Groups["scenario"].Value.ToUpperInvariant() : "VALID";

        calls.Record(Provider, scenario);

        switch (scenario)
        {
            case "DOWN":
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            case "SLOW":
                await Task.Delay(options.Value.SlowResponseDelay, cancellationToken);
                break;
        }

        return TypedResults.Ok(scenario switch
        {
            "INVALID" => Response("INVALID", faceMatch: true, confidence: 0.91),
            "INCONCLUSIVE" => Response("INCONCLUSIVE", faceMatch: true, confidence: 0.55),
            "NOFACE" => Response("VALID", faceMatch: false, confidence: 0.12),
            _ => Response("VALID", faceMatch: true, confidence: 0.97),
        });
    }

    private static IdentificationResponse Response(string documentResult, bool faceMatch, double confidence) =>
        new($"idn_{Guid.NewGuid():N}"[..16], documentResult, faceMatch, confidence);

    private static bool IsBase64(string? value) => !string.IsNullOrEmpty(value) && Base64.IsValid(value);

    [GeneratedRegex(@"IDNOW:(?<scenario>INVALID|INCONCLUSIVE|NOFACE|SLOW|DOWN)", RegexOptions.IgnoreCase)]
    private static partial Regex Marker();
}
