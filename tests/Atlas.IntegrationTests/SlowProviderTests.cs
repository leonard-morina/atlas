using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using static Atlas.IntegrationTests.AtlasApp;

namespace Atlas.IntegrationTests;

/// <summary>"A provider is slow to respond" (task README).</summary>
[TestClass]
public sealed class SlowProviderTests
{
    // The worry: a slow provider either holds the customer's request until the app gives up, or the request gives up
    // and the application is lost with it. Neither may happen: the submission answers within its budget, verification
    // carries on without it, and the decision is there when the app checks back.
    [TestMethod]
    public async Task A_slow_provider_gets_PROCESSING_within_the_budget_and_the_decision_arrives_later()
    {
        // "Slow": the World-Check stand-in answers after 8 seconds; the submission waits 5 (both set in AtlasApp).
        var started = Stopwatch.StartNew();
        var response = await Gateway.SubmitAsync(Applicants.Application("Slow"), Guid.NewGuid());
        var answeredAfter = started.Elapsed;
        var submission = await response.Content.ReadFromJsonAsync<SubmissionResponse>();

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.AreEqual("PROCESSING", submission!.Status);
        Assert.AreEqual($"/applications/{submission.ApplicationId}", response.Headers.Location?.OriginalString);
        Assert.IsLessThan(TimeSpan.FromSeconds(8), answeredAfter, "The submission must not wait for the slow provider.");

        // An approval moves on to account opening, so either status means the decision was made.
        var decided = await Gateway.WaitForStatusAsync(
            submission.ApplicationId, TimeSpan.FromSeconds(45), "APPROVED", "ACCOUNT_OPENED");

        Assert.Contains(decided.Status, new[] { "APPROVED", "ACCOUNT_OPENED" });
        Assert.IsNotNull(decided.DecidedAt);
    }
}
