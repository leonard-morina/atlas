using System.Net.Http.Json;
using static Atlas.IntegrationTests.AtlasApp;

namespace Atlas.IntegrationTests;

/// <summary>Account opening when core banking does not answer in time (CBS integration page §3, §4).</summary>
[TestClass]
public sealed class CoreBankingTimeoutTests
{
    // The failure I worry about most: OpenAccount has no idempotency, and a timeout does not mean it failed. Calling it
    // again would open a second account, which only a branch can close. The account must be found by looking it up,
    // and the customer must end up with exactly one.
    [TestMethod]
    public async Task An_OpenAccount_call_that_times_out_but_succeeds_ends_in_exactly_one_account()
    {
        // "Timeout": the core banking stand-in opens the account only after our call has given up (timings in AtlasApp).
        var response = await Gateway.SubmitAsync(Applicants.Application("Timeout"), Guid.NewGuid());
        var submission = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        Assert.AreEqual("APPROVED", submission!.Status);

        var application = await Gateway.WaitForStatusAsync(submission.ApplicationId, TimeSpan.FromSeconds(45), "ACCOUNT_OPENED");
        Assert.AreEqual("ACCOUNT_OPENED", application.Status, "The account opened behind the timeout must be found.");

        // Core banking's side: one account carries this application's reference, however many calls were made.
        var channelReference = submission.ApplicationId.ToString("N");
        var ledger = await Stubs.GetFromJsonAsync<CoreBankingLedger>("/_stub/corebanking");
        Assert.HasCount(1, ledger!.Accounts.Where(account => account.ChannelReference == channelReference));

        // Ours: one OpenAccount call, then lookups until the replica showed the account.
        var (openAttempts, confirmationChecks) = await AttemptsAsync(submission.ApplicationId);
        Assert.AreEqual(1, openAttempts, "OpenAccount must never be called again after a timeout.");
        Assert.IsGreaterThanOrEqualTo(1, confirmationChecks);
    }

    private static async Task<(int OpenAttempts, int ConfirmationChecks)> AttemptsAsync(Guid applicationId)
    {
        await using var database = await OpenDatabaseAsync("accounts-db");
        await using var command = database.CreateCommand();
        command.CommandText = "SELECT OpenAttempts, ConfirmationChecks FROM AccountOpenings WHERE ApplicationId = @applicationId";
        command.Parameters.AddWithValue("@applicationId", applicationId);
        await using var row = await command.ExecuteReaderAsync();
        Assert.IsTrue(await row.ReadAsync(), "The approval must have requested an account.");
        return (row.GetInt32(0), row.GetInt32(1));
    }

    private sealed record CoreBankingLedger(IReadOnlyList<CoreBankingAccount> Accounts);

    private sealed record CoreBankingAccount(string AccountNumber, string? ChannelReference);
}
