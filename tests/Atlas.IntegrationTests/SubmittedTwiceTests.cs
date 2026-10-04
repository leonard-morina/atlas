using System.Net;
using System.Net.Http.Json;
using static Atlas.IntegrationTests.AtlasApp;

namespace Atlas.IntegrationTests;

/// <summary>"The same application is submitted twice" (task README): the app retries, or the customer taps twice.</summary>
[TestClass]
public sealed class SubmittedTwiceTests
{
    // The case worth worrying about: retries arriving together, before the first is stored. A check before inserting
    // cannot see a request still in flight; only the database's unique index settles it. So this needs the real database.
    [TestMethod]
    public async Task The_same_submission_sent_five_times_at_once_stores_one_application()
    {
        var application = Applicants.Application("Twice");
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Gateway.SubmitAsync(application, key)));
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<SubmissionResponse>()));

        CollectionAssert.AreEqual(Enumerable.Repeat(HttpStatusCode.Created, 5).ToArray(), responses.Select(r => r.StatusCode).ToArray());
        Assert.HasCount(1, bodies.Select(body => body!.ApplicationId).Distinct(), "Every retry answers with the same application.");
        Assert.AreEqual(4, responses.Count(response => response.Headers.Contains("Idempotent-Replayed")), "All but the first are marked as replays.");
        Assert.AreEqual(1, await CountApplicationsAsync(key));
    }

    [TestMethod]
    public async Task The_same_key_with_different_content_is_refused()
    {
        var personalNumber = Applicants.NewPersonalNumber();
        var key = Guid.NewGuid();
        await Gateway.SubmitAsync(Applicants.Application("Twice", personalNumber), key);

        var response = await Gateway.SubmitAsync(Applicants.Application("Twice", personalNumber, phone: "+38970999999"), key);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.AreEqual(1, await CountApplicationsAsync(key));
    }

    [TestMethod]
    public async Task The_same_applicant_with_a_new_key_is_refused_while_the_first_application_stands()
    {
        var personalNumber = Applicants.NewPersonalNumber();
        var first = await Gateway.SubmitAsync(Applicants.Application("Twice", personalNumber), Guid.NewGuid());

        var second = await Gateway.SubmitAsync(Applicants.Application("Twice", personalNumber), Guid.NewGuid());

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

    private static async Task<int> CountApplicationsAsync(Guid idempotencyKey)
    {
        await using var database = await OpenDatabaseAsync("onboarding-db");
        await using var command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Applications WHERE IdempotencyKey = @key";
        command.Parameters.AddWithValue("@key", idempotencyKey);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
