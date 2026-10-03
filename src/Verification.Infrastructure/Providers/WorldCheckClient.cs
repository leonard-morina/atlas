using System.Net.Http.Json;
using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Domain.Checks;

namespace Atlas.Verification.Infrastructure.Providers;

/// <summary>Refinitiv World-Check's screening API, as documented in ATLAS-1.</summary>
internal sealed class WorldCheckClient(HttpClient http) : ISanctionsScreening
{
    public async Task<ScreeningCheck> ScreenAsync(
        string name,
        DateOnly dateOfBirth,
        string nationality,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "v2/screen", new ScreeningRequest(name, dateOfBirth, nationality), cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ScreeningResponse>(cancellationToken)
            ?? throw new InvalidOperationException("World-Check returned an empty screening.");

        return new ScreeningCheck(
            result.CaseId,
            result.Status switch
            {
                "CLEAR" => ScreeningStatus.Clear,
                "POSSIBLE_MATCH" => ScreeningStatus.PossibleMatch,
                _ => throw new InvalidOperationException($"World-Check returned unknown status '{result.Status}'."),
            },
            [.. result.Matches.Select(match => new ScreeningMatch(
                match.ListType == "PEP" ? WatchList.Pep : WatchList.Sanctions, match.Score))]);
    }

    private sealed record ScreeningRequest(string Name, DateOnly DateOfBirth, string Nationality);

    private sealed record ScreeningResponse(string CaseId, string Status, IReadOnlyList<Match> Matches);

    private sealed record Match(string ListType, double Score);
}
