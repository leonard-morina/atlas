using System.Text.RegularExpressions;
using Atlas.Stubs.Calls;
using Microsoft.Extensions.Options;

namespace Atlas.Stubs.WorldCheck;

public sealed record ScreeningRequest(string? Name, DateOnly? DateOfBirth, string? Nationality);

/// <param name="Status"><c>CLEAR</c> or <c>POSSIBLE_MATCH</c>.</param>
public sealed record ScreeningResponse(string CaseId, string Status, IReadOnlyList<ScreeningMatch> Matches);

/// <param name="ListType"><c>SANCTIONS</c> or <c>PEP</c>.</param>
public sealed record ScreeningMatch(string ListType, double Score, string Subject);

/// <summary>
/// Stands in for Refinitiv World-Check's <c>POST /v2/screen</c> (shape as in ATLAS-1). The scenario is a marker
/// word in the name: <c>Match</c> (sanctions hit), <c>Pep</c> (PEP hit), <c>Slow</c> or <c>Down</c>.
/// Any other name is clear.
/// </summary>
public static partial class WorldCheckEndpoints
{
    public const string Provider = "worldcheck";

    public static IEndpointRouteBuilder MapWorldCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/worldcheck/v2/screen", ScreenAsync);
        return endpoints;
    }

    private static async Task<IResult> ScreenAsync(
        ScreeningRequest request,
        CallLog calls,
        IOptions<StubOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.DateOfBirth is null || string.IsNullOrWhiteSpace(request.Nationality))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid screening request.");
        }

        var scenario = Marker().Match(request.Name) is { Success: true } match
            ? match.Groups["scenario"].Value.ToUpperInvariant()
            : "CLEAR";

        calls.Record(Provider, scenario);

        switch (scenario)
        {
            case "DOWN":
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            case "SLOW":
                await Task.Delay(options.Value.SlowResponseDelay, cancellationToken);
                break;
        }

        var caseId = $"wc_{Guid.NewGuid():N}"[..15];

        return TypedResults.Ok(scenario switch
        {
            "MATCH" => new ScreeningResponse(caseId, "POSSIBLE_MATCH", [new ScreeningMatch("SANCTIONS", 0.82, request.Name)]),
            "PEP" => new ScreeningResponse(caseId, "POSSIBLE_MATCH", [new ScreeningMatch("PEP", 0.77, request.Name)]),
            _ => new ScreeningResponse(caseId, "CLEAR", []),
        });
    }

    [GeneratedRegex(@"\b(?<scenario>Match|Pep|Slow|Down)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Marker();
}
