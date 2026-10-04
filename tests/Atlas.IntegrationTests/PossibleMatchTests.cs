using System.Net;
using System.Net.Http.Json;
using static Atlas.IntegrationTests.AtlasApp;

namespace Atlas.IntegrationTests;

/// <summary>"A provider returns a possible match instead of a clean result" (task README).</summary>
[TestClass]
public sealed class PossibleMatchTests
{
    // The worry: a possible sanctions match treated like a clean result, and a suspect gets a bank account. Compliance
    // §3: the application must be referred to a compliance officer, and the review may not be bypassed.
    [TestMethod]
    public async Task A_possible_match_is_referred_and_no_account_is_opened()
    {
        // "Match": the World-Check stand-in reports a possible sanctions match. A clean applicant goes alongside, to know
        // how long to wait: once its account is open, an opening wrongly requested for the referral would be there too.
        var referredSubmission = Gateway.SubmitAsync(Applicants.Application("Match"), Guid.NewGuid());
        var cleanSubmission = Gateway.SubmitAsync(Applicants.Application("Clean"), Guid.NewGuid());

        var response = await referredSubmission;
        var referred = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        var clean = await (await cleanSubmission).Content.ReadFromJsonAsync<SubmissionResponse>();

        // Not a final decision: an officer still has to decide, so the app checks back later.
        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.AreEqual("REFERRED", referred!.Status);

        var opened = await Gateway.WaitForStatusAsync(clean!.ApplicationId, TimeSpan.FromSeconds(30), "ACCOUNT_OPENED");
        Assert.AreEqual("ACCOUNT_OPENED", opened.Status, "The clean application sets the pace; it must get its account.");

        var application = await Gateway.GetFromJsonAsync<ApplicationStatusResponse>($"/applications/{referred.ApplicationId}");
        Assert.AreEqual("REFERRED", application!.Status, "Nothing may move a referral on but an officer.");
        Assert.AreEqual(0, await CountAccountOpeningsAsync(referred.ApplicationId));
    }

    private static async Task<int> CountAccountOpeningsAsync(Guid applicationId)
    {
        await using var database = await OpenDatabaseAsync("accounts-db");
        await using var command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM AccountOpenings WHERE ApplicationId = @applicationId";
        command.Parameters.AddWithValue("@applicationId", applicationId);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
