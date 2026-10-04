using Atlas.Accounts.Domain.Openings;

namespace Atlas.Accounts.UnitTests;

/// <summary>The opening's state machine: OpenAccount is called again only when the last call certainly did nothing.</summary>
[TestClass]
public sealed class AccountOpeningTests
{
    private static readonly Guid ApplicationId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(3);

    private static readonly Customer ByNationalId = new("Ana", "Petrović", new DateOnly(1991, 3, 4), "0403991450016", null, null);
    private static readonly Customer ByPassport = new("Omar", "Haddad", new DateOnly(1984, 12, 15), null, "X1234567", "SYR");

    [TestMethod]
    public void A_new_opening_is_queued_and_due_at_once()
    {
        var opening = Requested();

        Assert.AreEqual(OpeningStatus.Queued, opening.Status);
        Assert.AreEqual(Now, opening.DueAt);
    }

    [TestMethod]
    public void The_channel_reference_is_the_application_id_in_32_characters()
    {
        var opening = Requested();

        Assert.AreEqual("0f8fad5bd9cb469fa16570867728950e", opening.ChannelReference);
        Assert.HasCount(AccountOpening.ChannelReferenceLength, opening.ChannelReference);
    }

    [TestMethod]
    public void A_customer_needs_exactly_one_identifier()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            AccountOpening.Request(ApplicationId, "MF", ByNationalId with { PassportNumber = "X1234567" }, Now));
        Assert.ThrowsExactly<ArgumentException>(() =>
            AccountOpening.Request(ApplicationId, "MA", ByNationalId with { NationalId = null }, Now));
    }

    [TestMethod]
    public void Starting_a_queued_opening_calls_OpenAccount_under_a_lease()
    {
        var opening = Requested();

        opening.StartCall(Now, Lease);

        Assert.AreEqual(OpeningStatus.Opening, opening.Status);
        Assert.AreEqual(1, opening.OpenAttempts);
        Assert.AreEqual(Now + Lease, opening.LeaseExpiresAt);
    }

    [TestMethod]
    public void An_answered_call_opens_the_account()
    {
        var opening = InFlight();

        opening.RecordOpened("MA0000000001", Now.AddSeconds(40));

        Assert.AreEqual(OpeningStatus.Opened, opening.Status);
        Assert.AreEqual("MA0000000001", opening.AccountNumber);
        Assert.AreEqual(Now.AddSeconds(40), opening.CompletedAt);
        Assert.IsNull(opening.LeaseExpiresAt);
    }

    [TestMethod]
    public void A_refused_call_is_queued_again_for_later()
    {
        var opening = InFlight();

        opening.Defer(Now.AddSeconds(15));

        Assert.AreEqual(OpeningStatus.Queued, opening.Status);
        Assert.AreEqual(Now.AddSeconds(15), opening.DueAt);
        Assert.IsNull(opening.LeaseExpiresAt);
    }

    [TestMethod]
    public void A_validation_fault_goes_to_operations()
    {
        var opening = InFlight();

        opening.RecordRejected("Request rejected before processing.", Now);

        Assert.AreEqual(OpeningStatus.Escalated, opening.Status);
        Assert.AreEqual(EscalationReason.RejectedByCoreBanking, opening.EscalationReason);
    }

    [TestMethod]
    public void An_unknown_outcome_is_looked_up_later_and_never_opened_again_blindly()
    {
        var opening = InFlight();

        opening.RecordOutcomeUnknown("Timed out.", Now, checkAt: Now.AddSeconds(60));

        Assert.AreEqual(OpeningStatus.AwaitingConfirmation, opening.Status);
        Assert.AreEqual(Now.AddSeconds(60), opening.DueAt);

        opening.StartCall(Now.AddSeconds(60), Lease);

        Assert.AreEqual(OpeningStatus.Confirming, opening.Status);
        Assert.AreEqual(1, opening.OpenAttempts, "The next step is a lookup, not a second OpenAccount.");
        Assert.AreEqual(1, opening.ConfirmationChecks);
    }

    [TestMethod]
    public void An_unknown_outcome_for_a_passport_customer_goes_straight_to_operations()
    {
        var opening = InFlight(ByPassport, "MF");

        opening.RecordOutcomeUnknown("Timed out.", Now, checkAt: Now.AddSeconds(60));

        Assert.AreEqual(OpeningStatus.Escalated, opening.Status);
        Assert.AreEqual(EscalationReason.OutcomeCannotBeChecked, opening.EscalationReason);
    }

    [TestMethod]
    public void A_lookup_that_finds_the_account_opens_it()
    {
        var opening = Confirming();

        opening.RecordOpened("MA0000000007", Now);

        Assert.AreEqual(OpeningStatus.Opened, opening.Status);
        Assert.AreEqual("MA0000000007", opening.AccountNumber);
    }

    [TestMethod]
    public void A_lookup_that_does_not_find_it_checks_again_until_the_limit_then_escalates()
    {
        var opening = Confirming();

        opening.RecordNotFound(Now, Now.AddSeconds(60), maxChecks: 2);
        Assert.AreEqual(OpeningStatus.AwaitingConfirmation, opening.Status);

        opening.StartCall(Now.AddSeconds(60), Lease);
        opening.RecordNotFound(Now.AddSeconds(61), Now.AddSeconds(120), maxChecks: 2);

        Assert.AreEqual(OpeningStatus.Escalated, opening.Status);
        Assert.AreEqual(EscalationReason.OutcomeNotConfirmed, opening.EscalationReason);
        Assert.AreEqual(1, opening.OpenAttempts);
    }

    [TestMethod]
    public void A_refused_lookup_is_made_again_later()
    {
        var opening = Confirming();

        opening.Defer(Now.AddSeconds(15));

        Assert.AreEqual(OpeningStatus.AwaitingConfirmation, opening.Status);
        Assert.AreEqual(Now.AddSeconds(15), opening.DueAt);
    }

    [TestMethod]
    public void A_lost_OpenAccount_call_is_treated_as_an_unknown_outcome()
    {
        var opening = InFlight();

        opening.RecoverExpiredLease(Now + Lease, checkAt: Now + Lease + TimeSpan.FromSeconds(60));

        Assert.AreEqual(OpeningStatus.AwaitingConfirmation, opening.Status);
        Assert.AreEqual(Now + Lease + TimeSpan.FromSeconds(60), opening.DueAt);
    }

    [TestMethod]
    public void A_lost_lookup_is_simply_made_again()
    {
        var opening = Confirming();

        opening.RecoverExpiredLease(Now + Lease, checkAt: Now + Lease + TimeSpan.FromSeconds(60));

        Assert.AreEqual(OpeningStatus.AwaitingConfirmation, opening.Status);
        Assert.AreEqual(Now + Lease, opening.DueAt);
    }

    [TestMethod]
    public void A_live_lease_is_not_recovered() =>
        Assert.ThrowsExactly<InvalidOperationException>(() => InFlight().RecoverExpiredLease(Now.AddSeconds(30), Now));

    [TestMethod]
    public void A_finished_opening_takes_no_more_steps()
    {
        var opening = InFlight();
        opening.RecordOpened("MA0000000001", Now);

        Assert.ThrowsExactly<InvalidOperationException>(() => opening.StartCall(Now, Lease));
        Assert.ThrowsExactly<InvalidOperationException>(() => opening.Defer(Now));
        Assert.ThrowsExactly<InvalidOperationException>(() => opening.RecordOpened("MA0000000002", Now));
    }

    private static AccountOpening Requested(Customer? customer = null, string market = "MA") =>
        AccountOpening.Request(ApplicationId, market, customer ?? ByNationalId, Now);

    private static AccountOpening InFlight(Customer? customer = null, string market = "MA")
    {
        var opening = Requested(customer, market);
        opening.StartCall(Now, Lease);
        return opening;
    }

    private static AccountOpening Confirming()
    {
        var opening = InFlight();
        opening.RecordOutcomeUnknown("Timed out.", Now, checkAt: Now);
        opening.StartCall(Now, Lease);
        return opening;
    }
}
