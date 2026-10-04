using System.Net.Http.Json;

namespace Atlas.IntegrationTests;

/// <summary>
/// Submissions as mobile sends them. Every applicant gets a fresh, valid identifier, so tests never collide with each
/// other or with earlier runs: an identifier can have only one open application per market.
/// The last name drives the provider stand-ins (World-Check: Slow, Match; core banking: Timeout, Busy, Invalid, Eod).
/// </summary>
public static class Applicants
{
    public static object Application(string lastName, string? personalNumber = null, string phone = "+38970123456") => new
    {
        firstName = "Test",
        lastName,
        dateOfBirth = "1985-06-15",
        country = "MA",
        nationality = "MKD",
        identifier = new { type = "NATIONAL_ID", value = personalNumber ?? NewPersonalNumber() },
        email = "test@example.com",
        phone,
        documents = new[]
        {
            new { type = "PASSPORT", image = "aGVsbG8=" },
            new { type = "SELFIE", image = "aGVsbG8=" },
        },
        termsAccepted = true,
    };

    public static Task<HttpResponseMessage> SubmitAsync(this HttpClient gateway, object application, Guid idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/applications") { Content = JsonContent.Create(application) };
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return gateway.SendAsync(request);
    }

    /// <summary>
    /// Asks <c>GET /applications/{id}</c>, as mobile does, until the status is one of <paramref name="statuses"/>.
    /// Gives up after <paramref name="timeout"/> with the last status seen.
    /// </summary>
    public static async Task<ApplicationStatusResponse> WaitForStatusAsync(
        this HttpClient gateway, Guid applicationId, TimeSpan timeout, params string[] statuses)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var application = await gateway.GetFromJsonAsync<ApplicationStatusResponse>($"/applications/{applicationId}");
            if (statuses.Contains(application!.Status) || DateTimeOffset.UtcNow > deadline)
            {
                return application;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }

    /// <summary>
    /// A Personal Number for MA (CDS-ID-04 §3.1) for someone born 15 June 1985: DDMMYYY, a region, a random serial and
    /// the check digit. The serial and region give 90,000 distinct numbers; a test picking a used one would get 409.
    /// </summary>
    public static string NewPersonalNumber()
    {
        while (true)
        {
            var digits = $"1506985{Random.Shared.Next(10, 100)}{Random.Shared.Next(0, 1000):D3}".Select(c => c - '0').ToArray();
            var sum = 7 * (digits[0] + digits[6]) + 6 * (digits[1] + digits[7]) + 5 * (digits[2] + digits[8])
                      + 4 * (digits[3] + digits[9]) + 3 * (digits[4] + digits[10]) + 2 * (digits[5] + digits[11]);
            var check = 11 - sum % 11;

            // 10 has no check digit: no such number is issued.
            if (check == 10)
            {
                continue;
            }

            return string.Concat(digits) + (check == 11 ? 0 : check);
        }
    }
}

public sealed record SubmissionResponse(Guid ApplicationId, string Status);

public sealed record ApplicationStatusResponse(Guid ApplicationId, string Status, DateTimeOffset SubmittedAt, DateTimeOffset? DecidedAt);
