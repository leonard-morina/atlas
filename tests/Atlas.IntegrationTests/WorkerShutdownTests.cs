using System.Net.Http.Json;
using static Atlas.IntegrationTests.AtlasApp;

namespace Atlas.IntegrationTests;

/// <summary>A deployment while work is in progress: workers are stopped with a signal and must finish what they started.</summary>
[TestClass]
public sealed class WorkerShutdownTests
{
    // Stopping a worker mid-OpenAccount and cutting the call off would be safe (the account is looked up later), but it
    // throws away a call that was about to succeed. A stopping worker takes nothing new and finishes its calls in flight;
    // the restarted worker carries on, and the customer still gets exactly one account.
    [TestMethod]
    [DoNotParallelize] // Stops the Accounts worker, which the other tests need.
    public async Task A_worker_stopped_mid_call_finishes_the_call_and_the_restarted_worker_carries_on()
    {
        var response = await Gateway.SubmitAsync(Applicants.Application("Deployed"), Guid.NewGuid());
        var submission = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.AreEqual("APPROVED", submission!.Status);

        // OpenAccount takes 2 s in the stand-in (AtlasApp): stop the worker as soon as the call has started.
        await WaitForOpeningAsync(submission.ApplicationId, "Opening");
        await StopServiceAsync("accounts-worker");

        // Finished before the worker exited: opened, without needing a single lookup. Cut off, it would be waiting for one.
        var stopped = await OpeningAsync(submission.ApplicationId);
        Assert.AreEqual("Opened", stopped.Status, "The call in flight must be finished, not cut off.");
        Assert.AreEqual(0, stopped.ConfirmationChecks);

        // The AccountOpened event waited in the outbox while the worker was down; it is delivered once it is back.
        await StartServiceAsync("accounts-worker");
        var application = await Gateway.WaitForStatusAsync(submission.ApplicationId, TimeSpan.FromSeconds(30), "ACCOUNT_OPENED");
        Assert.AreEqual("ACCOUNT_OPENED", application.Status);

        var ledger = await Stubs.GetFromJsonAsync<CoreBankingLedger>("/_stub/corebanking");
        Assert.HasCount(1, ledger!.Accounts.Where(account => account.ChannelReference == submission.ApplicationId.ToString("N")));
        Assert.AreEqual(1, stopped.OpenAttempts);
    }

    private static async Task WaitForOpeningAsync(Guid applicationId, string status)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while ((await OpeningAsync(applicationId)).Status != status)
        {
            Assert.IsLessThan(deadline, DateTimeOffset.UtcNow, $"The opening never reached {status}.");
            await Task.Delay(100);
        }
    }

    private static async Task<(string Status, int OpenAttempts, int ConfirmationChecks)> OpeningAsync(Guid applicationId)
    {
        await using var database = await OpenDatabaseAsync("accounts-db");
        await using var command = database.CreateCommand();
        command.CommandText =
            "SELECT Status, OpenAttempts, ConfirmationChecks FROM AccountOpenings WHERE ApplicationId = @applicationId";
        command.Parameters.AddWithValue("@applicationId", applicationId);
        await using var row = await command.ExecuteReaderAsync();
        return await row.ReadAsync() ? (row.GetString(0), row.GetInt32(1), row.GetInt32(2)) : ("(none)", 0, 0);
    }

    private sealed record CoreBankingLedger(IReadOnlyList<CoreBankingAccount> Accounts);

    private sealed record CoreBankingAccount(string AccountNumber, string? ChannelReference);
}
