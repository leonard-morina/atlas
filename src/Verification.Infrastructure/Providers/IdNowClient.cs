using System.Net.Http.Json;
using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Application.VerifyApplication;
using Atlas.Verification.Domain.Checks;

namespace Atlas.Verification.Infrastructure.Providers;

/// <summary>IDNow's identification API, as documented in ATLAS-1.</summary>
internal sealed class IdNowClient(HttpClient http) : IIdentityVerification
{
    public async Task<IdentityCheck> VerifyAsync(
        IdentityDocumentType documentType,
        byte[] document,
        byte[] selfie,
        CancellationToken cancellationToken)
    {
        var request = new IdentificationRequest(
            documentType == IdentityDocumentType.Passport ? "PASSPORT" : "ID_CARD",
            Convert.ToBase64String(document),
            Convert.ToBase64String(selfie));

        using var response = await http.PostAsJsonAsync("v1/identifications", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IdentificationResponse>(cancellationToken)
            ?? throw new InvalidOperationException("IDNow returned an empty identification.");

        return new IdentityCheck(
            result.IdentificationId,
            result.DocumentResult switch
            {
                "VALID" => DocumentResult.Valid,
                "INVALID" => DocumentResult.Invalid,
                "INCONCLUSIVE" => DocumentResult.Inconclusive,
                _ => throw new InvalidOperationException($"IDNow returned unknown document result '{result.DocumentResult}'."),
            },
            result.FaceMatch,
            result.Confidence);
    }

    private sealed record IdentificationRequest(string DocumentType, string DocumentImage, string SelfieImage);

    private sealed record IdentificationResponse(string IdentificationId, string DocumentResult, bool FaceMatch, double Confidence);
}
