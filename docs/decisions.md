# Decisions

What I decided, why, and what I rejected. Newest decisions are added at the end.

---

## API versioning: deliberately ahead of need

**Decision.** The Onboarding API is versioned in its route (`/v1/applications`) with
[`Asp.Versioning`](https://github.com/dotnet/aspnet-api-versioning). Mobile never sees the version:
the gateway exposes the unversioned path from the ticket, `POST /applications`, and maps it to
`/v1/applications`.

**Honest note.** With one version, this exercise does not need versioning. I would normally add it
when a second version is actually coming. I included it because how an API contract evolves is part
of what this task asks about, and because this contract is already under pressure: mobile were told it
would not change, and it has to (see the note to the mobile team).

## Gateway: YARP in front of the services

**Decision.** A small [YARP](https://github.com/dotnet/yarp) gateway (`src/Gateway`) is the only
externally reachable service. Its route table (`appsettings.json`) is the public API: only routes listed
there exist from the outside, so versioned paths, health checks and OpenAPI documents stay internal.

**Why a gateway with one public service.** The audiences differ: mobile now, compliance officers next
(the manual review that Compliance 3 requires), and they should not share a surface. Edge concerns
(request size limits for the document images, rate limiting) belong in one place. YARP runs as a normal
.NET project with `dotnet run`; Nginx/Envoy would need images outside the allowed list, and in Azure the
AKS ingress or API Management could take over this role.

## A verdict for a market that is no longer configured

**Decision.** If Verification's verdict arrives for an application whose market has since been removed from the
`Markets` configuration (a deployment in between), Onboarding refuses to record it. The message is retried and then
lands in the error queue, where a person decides what happens to that application.

**Honest note.** This is a business decision, not a technical one, and the requirements do not answer it. I chose the
conservative option: an application is never quietly approved, rejected or dropped under rules that no longer apply.
It is in the open questions for Product and Compliance.

## Referred applications: no review step in v1

**Decision.** A possible sanctions or PEP match ends in `REFERRED`, and nothing in this system moves it on. No account
is opened. Mobile is told the review can take up to 48 hours and is never told why (that would be tipping off).
`VerificationCompleted` carries the World-Check case id, so a compliance officer knows which case to review.

**Why not build the review.** The requirements contradict each other. Compliance 3: possible matches "must be referred
for manual review by a compliance officer in the relevant market ... Under no circumstances may this review be automated
or bypassed." ATLAS-1, out of scope for v1: "Anything involving a human reviewing anything." I follow Compliance, because
it is a legal obligation and the ticket is a product choice: possible matches are stopped and referred. I do not build the
officers' side, because the ticket rules it out, and because officers most likely work in a case-management tool
(World-Check has its own case workflow). How that tool's decision reaches us is the real open question.

**Honest note.** Until that is answered, a referred application stays `REFERRED`. It is listed under known limitations
and in the open questions for Product and Compliance.

**If it were built.** It is small, because approving already leads into account opening. A sketch:

```csharp
// Onboarding.Domain: OnboardingApplication
public void RecordComplianceDecision(ComplianceDecision decision, OfficerId officer, string reason, DateTimeOffset at)
{
    if (Status != ApplicationStatus.Referred) throw ...;          // only referred applications are reviewed
    Status = decision == Approve ? ApplicationStatus.Approved      // MD: AwaitingBranchVisit, as for any approval
                                 : ApplicationStatus.Rejected;
    Review = new ComplianceReview(officer, decision, reason, at);  // audit: who, what, why; kept 10 years (Compliance 4)
}

// Onboarding.Application: RecordComplianceDecisionHandler (MediatR command)
var application = await applications.FindForUpdateAsync(command.ApplicationId);
application.RecordComplianceDecision(command.Decision, command.Officer, command.Reason, now);
if (application.NeedsAccount)
    await publishEndpoint.Publish(new AccountOpeningRequested(...));   // same as an automatic approval
await applications.SaveChangesAsync();                                 // decision and message together (outbox)

// Entry point, one of:
//  - a consumer of the case-management tool's "case closed" event (preferred: officers keep their tool), or
//  - POST /compliance/applications/{id}/decision on a separate gateway route for officers, behind
//    authentication with an officer role scoped to the application's market (Compliance 3: "in the relevant market").
```

The Accounts service needs no change: it opens an account for any `AccountOpeningRequested`, whoever approved it.

## Waking a waiting submission: a broadcast through the broker, after commit

**Decision.** A submission waits up to 10 seconds for its decision (mobile wants one call, one answer). It no longer polls
the database every 100 ms for that, which was up to 100 queries per submission. It sleeps until the decision is
announced, then reads it once. The decision may be recorded by one API instance while the HTTP request waits on another,
so the announcement reaches every instance: each has its own RabbitMQ queue for it, created at startup and deleted by the
broker when the instance's connection closes (`AddBroadcastConsumer` in the Messaging building block).

**Announced only after the commit.** Recording the decision publishes an `ApplicationDecided` event through the outbox,
so it is delivered only once the decision is committed, and Onboarding consumes its own event to wake the request.
Announcing from inside the handler would be earlier: with read committed snapshot (the default in Azure SQL Database), the
woken request could read the application before the decision is visible, and wait out its budget for nothing.

**A hint, never the record.** The decision is always read from the database. A lost announcement (the broker restarting,
an instance's queue being recreated) costs at most one fallback poll, every second. The broadcast queues skip the retry
and inbox/outbox every other endpoint has: they only wake a request in memory, so there is nothing to make atomic or to
deduplicate.

**Rejected.** An in-process signal (as Accounts uses) cannot reach another instance. Redis pub/sub works too, and was the
first version; it took an extra hop (event to one instance, then Redis to all) and an extra dependency for a signal the
broker can already deliver. Redis stays for what only it does here: shared counters for rate limiting at the gateway.
Caching application status was rejected: the lookup is one primary-key read, and the status changes within seconds,
which is exactly what the client is asking about.
