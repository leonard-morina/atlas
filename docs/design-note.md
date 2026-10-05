# Design note

## What I built

The mandatory path, end to end: `POST /applications` → both providers → an answer, for all six markets, plus account
opening in core banking. Four services, each with its own database, talking over RabbitMQ:

- **Gateway**: the only public entry. Unversioned paths, rate limit on submissions, 28 MB body limit.
- **Onboarding**: validates per market, stores the application and the images, answers mobile. Owns the status.
- **Verification**: calls IDNow and World-Check at the same time and decides approved / rejected / referred.
- **Accounts**: opens the account in core banking without ever opening two.

The ticket wants one call and one answer, but the providers are external and sometimes slow. So the inside is
asynchronous (messages, outbox) and the submission **waits up to 10 seconds** for the decision. Normally it comes
within a second and mobile gets `201 APPROVED/REJECTED`. When it doesn't, mobile gets `202` and polls. Nothing is lost
if the wait runs out, the request just stops waiting.

The three required cases: a **slow provider** answers `202 PROCESSING`, the decision lands later. A **possible match**
is referred to a person, never rejected or approved automatically. The **same application twice** gives the same
application back (Idempotency-Key, and the database's unique indexes decide races, even five requests at once).

## Where I didn't build it as described

| The material says | What I did instead | Why |
|---|---|---|
| A possible match "just gets rejected" (Teams), "no human review" (ticket, AC3) | `REFERRED`, waits for a compliance officer | Compliance 3: the review is required and can't be automated |
| Final answer in under 3 minutes (AC2), account + card when the call returns (AC6) | answer within seconds for most; account opened right after; no card | a referral takes up to 48 h, core banking takes 20–90 s and closes at night, there's no card system |
| All six markets without a branch (AC1) | MD ends at `AWAITING_BRANCH_VISIT` | Annex B: MD needs a signature in person, the exemption is two years pending |
| `nationalId`, one string | `identifier { type, value, issuingCountry }` + `nationality` | MF residents onboard with a passport; screening needs nationality, not residence |

## What I deliberately didn't build

The officer's review of a referral (out of scope in the ticket, sketched in decisions.md), card ordering, real
authentication, save-and-resume beyond retries, push notifications, and anything for production deployment (Dockerfiles,
Kubernetes, Service Bus, managed identities, a migration pipeline). Also not built: the compliance requirements that
need a decision first (below).

## The order I'd do the rest in

1. **Get Compliance's answers on data residency and access** (Compliance 1, 2). They decide how and where this gets deployed,
   and both are expensive to change once there's real data (see below). Nothing goes live before that.
2. **Authentication and access logging.** Customers authenticated (the API is anonymous now), every service with its
   own identity, every read of personal data logged (Compliance 2). Also makes the rate limit per customer instead of per IP.
3. **The referral loop.** How the officer's decision comes back (from World-Check's case tool, ideally) and moves the
   application on. Without it referrals never finish.
4. **Operations' side of account opening.** Escalations are published but nobody reads them yet.
5. **Production deployment.** Service Bus instead of RabbitMQ (a config change), managed identities, migrations from
   the pipeline, 3 replicas (the design already runs fine with several).
6. **Mobile's wishes:** push notifications instead of polling, a document upload before submit for save-and-resume.
7. **Card ordering**, once there's a card system to talk to.

Compliance first because it's the only thing that can change the architecture. The rest are features that fit in it.

## Structural decisions, and what reversing them would cost

**1. Data residency: one deployment for all six markets.** Compliance 1 says personal data must not leave the
customer's country, Platform says one region, one SQL instance. Today only the images are separated per market (blob
path starts with the market), the database rows, messages and logs aren't. If Compliance says "in-country", the fix is
one deployment per market from the same codebase, or per-market storage everywhere. **Cost with production data:
very high**. Every stored application, image, message and log line has to be moved into the right country and
re-pointed, while staying retained for ten years. That's why I'd settle it before go-live, not after.

**2. Services and a database each, joined by messages.** Splitting it this way makes slow providers and core banking
someone else's problem than the request's, and lets each part fail and retry on its own. **Cost to reverse: medium.**
Merging the databases is a data migration and the message flows become calls, but no data is lost or reshaped. The
data model inside each service matters more:

**3. Identifiers are inputs, not keys.** An application has its own id; the identifier is stored as `type + value +
issuing country` and only used to stop the same person applying twice in a market (Annex B note 2, and Sam's warning in
Teams). **Cost to reverse: high.** Had I keyed on the national ID, MF passport customers wouldn't fit, and changing keys
later touches every table and every message that refers to an application.

## The part I'd least want someone else to change

The rule in Accounts that `OpenAccount` is only ever called again when the last call certainly did nothing
(`AccountOpening` + `CallCoreBankingHandler`). It looks like it would be simpler to just retry, and that's exactly how
a customer ends up with two accounts that only a branch can close.
