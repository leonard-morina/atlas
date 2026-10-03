using System.Security.Cryptography;
using System.Text.Json;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

/// <summary>
/// A SHA-256 over everything submitted except the Idempotency-Key, so a retry can be told apart from a different
/// application sent with a reused key. Images contribute their own hash, not their bytes.
/// </summary>
internal static class SubmissionFingerprint
{
    public static string Of(SubmitApplicationCommand command)
    {
        var content = new
        {
            command.FirstName,
            command.LastName,
            command.DateOfBirth,
            command.Market,
            command.Nationality,
            command.Identifier,
            command.Email,
            command.Phone,
            Documents = command.Documents.Select(document => new
            {
                document.Type,
                Sha256 = Convert.ToHexString(SHA256.HashData(document.Content)),
            }),
        };

        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
    }
}
