namespace Atlas.Accounts.Domain.Openings;

/// <summary>
/// Opening one approved applicant's current account in core banking, the aggregate root. One per application.
/// OpenAccount has no idempotency (CBS §3) and a timeout does not mean it failed (CBS §4), so OpenAccount is only
/// called again when the previous call certainly did nothing; when that is unknown, the account is looked up
/// instead, and when it cannot be, a person decides.
/// </summary>
public sealed class AccountOpening
{
    /// <summary>Core banking stores at most 32 characters of ChannelReference (CBS §2).</summary>
    public const int ChannelReferenceLength = 32;

    private AccountOpening(Guid applicationId, string market, Customer customer, DateTimeOffset requestedAt)
    {
        ApplicationId = applicationId;
        Market = market;
        Customer = customer;
        ChannelReference = ChannelReferenceFor(applicationId);
        Status = OpeningStatus.Queued;
        RequestedAt = requestedAt;
        DueAt = requestedAt;
    }

    // Used by persistence to rebuild a stored opening.
    private AccountOpening()
    {
        Market = null!;
        Customer = null!;
        ChannelReference = null!;
    }

    /// <summary>One opening per application: the application's id is the key.</summary>
    public Guid ApplicationId { get; private set; }

    public string Market { get; private set; }

    public Customer Customer { get; private set; }

    /// <summary>
    /// Sent with OpenAccount and returned by FindCustomerAccounts: how an account this service opened is told apart
    /// from the customer's other accounts. Core banking does not use it to detect duplicates (CBS §2).
    /// </summary>
    public string ChannelReference { get; private set; }

    public OpeningStatus Status { get; private set; }

    /// <summary>When the next call may be made, while <see cref="OpeningStatus.Queued"/> or awaiting confirmation.</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>
    /// While a call is in flight: when it is presumed lost (the worker making it stopped). Longer than the call's own
    /// timeout, so a live call always reports first.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public int OpenAttempts { get; private set; }

    public int ConfirmationChecks { get; private set; }

    public string? AccountNumber { get; private set; }

    public EscalationReason? EscalationReason { get; private set; }

    public string? EscalationDetail { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>When the opening was finished: opened or escalated.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsInFlight => Status is OpeningStatus.Opening or OpeningStatus.Confirming;

    public static AccountOpening Request(Guid applicationId, string market, Customer customer, DateTimeOffset requestedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(applicationId, Guid.Empty);

        if ((customer.NationalId is null) == (customer.PassportNumber is null))
        {
            throw new ArgumentException("A customer is identified by a national ID or a passport, exactly one.", nameof(customer));
        }

        return new AccountOpening(applicationId, market, customer, requestedAt);
    }

    /// <summary>The application's id without hyphens: 32 characters, exactly what core banking stores.</summary>
    public static string ChannelReferenceFor(Guid applicationId) => applicationId.ToString("N");

    /// <summary>Takes the next step: an OpenAccount call when queued, a lookup when awaiting confirmation.</summary>
    /// <param name="lease">How long the call may take before it is presumed lost.</param>
    public void StartCall(DateTimeOffset now, TimeSpan lease)
    {
        switch (Status)
        {
            case OpeningStatus.Queued:
                Status = OpeningStatus.Opening;
                OpenAttempts++;
                break;
            case OpeningStatus.AwaitingConfirmation:
                Status = OpeningStatus.Confirming;
                ConfirmationChecks++;
                break;
            default:
                throw InvalidTransition(nameof(StartCall));
        }

        LeaseExpiresAt = now + lease;
    }

    /// <summary>OpenAccount answered with an account, or a lookup found the one this service opened.</summary>
    public void RecordOpened(string accountNumber, DateTimeOffset now)
    {
        EnsureInFlight(nameof(RecordOpened));

        AccountNumber = accountNumber;
        Complete(OpeningStatus.Opened, now);
    }

    /// <summary>
    /// The call was refused before core banking did anything (CONCURRENCY_LIMIT, EOD_IN_PROGRESS, or the connection
    /// was never made): the same step is taken again at <paramref name="retryAt"/>.
    /// </summary>
    public void Defer(DateTimeOffset retryAt)
    {
        Status = Status switch
        {
            OpeningStatus.Opening => OpeningStatus.Queued,
            OpeningStatus.Confirming => OpeningStatus.AwaitingConfirmation,
            _ => throw InvalidTransition(nameof(Defer)),
        };

        DueAt = retryAt;
        LeaseExpiresAt = null;
    }

    /// <summary>Core banking refused the request as invalid (VALIDATION): the same data cannot succeed (CBS §7).</summary>
    public void RecordRejected(string detail, DateTimeOffset now)
    {
        EnsureInFlight(nameof(RecordRejected));
        Escalate(Openings.EscalationReason.RejectedByCoreBanking, detail, now);
    }

    /// <summary>
    /// OpenAccount ended without an answer: a timeout, a dropped connection, a reply that could not be read. The
    /// account may exist, so it is looked up once the replica has caught up, at <paramref name="checkAt"/>; never
    /// opened again blindly. A customer who cannot be looked up goes to operations straight away.
    /// </summary>
    public void RecordOutcomeUnknown(string detail, DateTimeOffset now, DateTimeOffset checkAt)
    {
        if (Status != OpeningStatus.Opening)
        {
            throw InvalidTransition(nameof(RecordOutcomeUnknown));
        }

        if (!Customer.CanBeLookedUp)
        {
            Escalate(Openings.EscalationReason.OutcomeCannotBeChecked, detail, now);
            return;
        }

        Status = OpeningStatus.AwaitingConfirmation;
        DueAt = checkAt;
        LeaseExpiresAt = null;
    }

    /// <summary>
    /// The lookup did not show the account. It may still be on its way (the call can outlive the client's timeout,
    /// and the replica lags), so it is checked again at <paramref name="nextCheckAt"/>, up to
    /// <paramref name="maxChecks"/> times. After that it goes to operations rather than risk a second account.
    /// </summary>
    public void RecordNotFound(DateTimeOffset now, DateTimeOffset nextCheckAt, int maxChecks)
    {
        if (Status != OpeningStatus.Confirming)
        {
            throw InvalidTransition(nameof(RecordNotFound));
        }

        if (ConfirmationChecks >= maxChecks)
        {
            Escalate(
                Openings.EscalationReason.OutcomeNotConfirmed,
                $"The account did not appear in {ConfirmationChecks} lookups.",
                now);
            return;
        }

        Status = OpeningStatus.AwaitingConfirmation;
        DueAt = nextCheckAt;
        LeaseExpiresAt = null;
    }

    /// <summary>
    /// The worker making a call stopped before it reported (the lease ran out). An OpenAccount call may have
    /// reached core banking, so its outcome is unknown; a lookup has no side effects and is simply made again.
    /// </summary>
    public void RecoverExpiredLease(DateTimeOffset now, DateTimeOffset checkAt)
    {
        if (!IsInFlight || LeaseExpiresAt > now)
        {
            throw InvalidTransition(nameof(RecoverExpiredLease));
        }

        if (Status == OpeningStatus.Opening)
        {
            RecordOutcomeUnknown("The worker making the OpenAccount call stopped before it answered.", now, checkAt);
            return;
        }

        Defer(now);
    }

    private void Escalate(EscalationReason reason, string detail, DateTimeOffset now)
    {
        EscalationReason = reason;
        EscalationDetail = detail;
        Complete(OpeningStatus.Escalated, now);
    }

    private void Complete(OpeningStatus status, DateTimeOffset now)
    {
        Status = status;
        CompletedAt = now;
        LeaseExpiresAt = null;
    }

    private void EnsureInFlight(string transition)
    {
        if (!IsInFlight)
        {
            throw InvalidTransition(transition);
        }
    }

    private InvalidOperationException InvalidTransition(string transition) =>
        new($"Account opening for application {ApplicationId} cannot {transition} while {Status}.");
}
