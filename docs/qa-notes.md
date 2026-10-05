# Notes for QA

Everything goes through the gateway, `http://localhost:5070`. Every request below is also in `requests/atlas.http`,
ready to send. Start the system with `docker compose up -d` and `dotnet run --project src/AppHost`.

## The scenarios, the ones I'd be most nervous about first

**1. Same application sent twice (also at the same time).**
Start: no application for this person. Send the same body with the same `Idempotency-Key` several times, also in
parallel.
After: every answer is the same, same `applicationId`, and all except the first have the header
`Idempotent-Replayed: true`. There is exactly one application stored, IDNow and World-Check were called once
(`GET :5090/_stub/calls`), and later there is one account.
Variants: same key with a different body → `422`. Same person with a new key while the first application is still
open → `409`. A person who was `REJECTED` may apply again → `201`.

**2. A possible sanctions/PEP match.**
Last name `Match` (sanctions) or `Pep`. Answer `202` with `REFERRED`, and it stays `REFERRED`, nothing in the system
moves it on (only an officer may, and that's not built). There must be **no account**: nothing in
`GET :5090/_stub/corebanking` for it. The answers never say why (no reasons, no match details), telling someone they
matched a sanctions list is tipping off.

**3. Core banking times out but opens the account anyway.**
Last name `Timeout`. `201 APPROVED`, then about 80 seconds later `GET /applications/{id}` shows `ACCOUNT_OPENED`.
In `GET :5090/_stub/corebanking` there is exactly one account with `channelReference` = the application id without
dashes, and `duplicates` is empty. If you ever see two accounts for one person, that's the worst bug we can have.

**4. Slow provider.**
Last name `Slow` (World-Check takes 20 s). Answer `202 PROCESSING` after about 10 s, not later. A bit later the GET
shows `APPROVED` (then `ACCOUNT_OPENED`). Providers still called once.

**5. Normal path per market.** `201 APPROVED`, a few seconds later `ACCOUNT_OPENED`. Except **MD**: `201
AWAITING_BRANCH_VISIT` and no account, the customer has to sign in a branch first.

**6. Rejections.** Document image containing `IDNOW:INVALID`, `IDNOW:INCONCLUSIVE` or `IDNOW:NOFACE` → `201 REJECTED`,
no account.

**7. Account opening problems** (approved first, then):

| Last name | What happens | What you see |
|---|---|---|
| `Busy` | core banking says CONCURRENCY_LIMIT twice | retried, `ACCOUNT_OPENED` a bit later |
| `Invalid` | core banking says VALIDATION | not retried, stays `APPROVED`, handed to operations (see "can't test through the API") |
| `Eod` | core banking says end of day | retried every minute, stays `APPROVED` |
| `Timeout` + MF passport | can't be looked up by passport | stays `APPROVED`, handed to operations, never opened twice |

**8. Bad requests.**
`400` = we couldn't read it (broken JSON, unknown enum value, missing or non-UUID `Idempotency-Key`), the error says
which field. `422` = we read it but it's not valid, errors per field. Things that must be `422`: identifier with a
wrong check digit (the ticket's own example `0403991450016` is one!), identifier that doesn't match the date of birth,
passport outside MF, passport without issuing country, not exactly one ID document + one selfie, image over 10 MB,
terms not accepted.

**9. The edge.** More than 10 submissions a minute from one address → `429` with `Retry-After`. Body over 28 MB →
`413`. `/v1/applications` through the gateway → `404` (versions are internal).

**10. A provider is down.** Image with `IDNOW:DOWN` (or name `Down` for World-Check) → `202 PROCESSING` and it stays
like that, the message is retried for about a minute and then lands in RabbitMQ's `application-submitted_error`
queue for someone to look at. No crash, nothing half-saved.

## How to drive it

The stand-ins pick their behaviour from markers in the request:

| Provider | Where the marker goes | Markers |
|---|---|---|
| IDNow | inside the document image (base64 of e.g. `IDNOW:INVALID`) | `IDNOW:INVALID`, `IDNOW:INCONCLUSIVE`, `IDNOW:NOFACE`, `IDNOW:SLOW`, `IDNOW:DOWN` |
| World-Check | a word in the first/last name | `Match`, `Pep`, `Slow`, `Down` |
| Core banking | a word in the last name | `Timeout`, `Busy`, `Invalid`, `Eod` |

No marker = everything goes well. What the stand-ins received: `GET :5090/_stub/calls` and
`GET :5090/_stub/corebanking` (`DELETE` on both resets them).

**Data per market.** An identifier can only have one open application per market, so you need a fresh one for each
run (or reset the databases). Valid examples:

| Market | Identifier | Example | Notes |
|---|---|---|---|
| MA, MB | Personal Number, 13 digits | `0403991450014` (born 1991-03-04) | first 7 digits are the birth date, last is a check digit |
| MC | Unified Citizen ID, 9 chars | `48215731A` | check letter at the end |
| MD | Citizen Number, 10 digits | `7321459080`, `5849301275` | check digit; approval means branch visit |
| ME | Civil Number, 10 digits | any 10 digits | we only check the format, the algorithm isn't documented |
| MF | Personal Number or passport | `1512984702013` (born 1984-12-15), or passport `N7654321` + issuing country `SYR` | only MF takes passports |

For lots of fresh MA/MB/MF numbers: `NewPersonalNumber()` in `tests/Atlas.IntegrationTests/Applicants.cs` shows the
algorithm. Slow things go faster locally if you change the timings in `src/Stubs/appsettings.json` and
`src/Accounts.Worker/appsettings.Development.json`.

## What can't be tested through the API

- **What's stored and side effects** (one row, one provider call, one account): the public API only shows the status.
  Use the stand-ins' endpoints above, or the databases (`atlas_onboarding`, `atlas_verification`, `atlas_accounts`,
  user `sa`, password in `.env`). The integration tests check these directly.
- **Escalations to operations** (account opening gave up): not visible anywhere in the API. In `atlas_accounts`,
  `AccountOpenings.Status = 'Escalated'` with the reason, and an `AccountOpeningEscalated` message on RabbitMQ.
- **Crashes and deployments**: kill a worker in the middle of something and check nothing is lost or done twice. The
  integration test `WorkerShutdownTests` does it for account opening, manually: `kill -TERM <pid>` (finishes its work
  first) or `kill -9` (redelivered later, still done once).
- **The core banking limit (max 2 of our calls per market)** and **several copies of each service**: run with
  `--Atlas:Replicas=3`, send many at once, then `peakInFlight` in `/_stub/corebanking` must never go over 2.
- **The night window (22:00–06:00 market time)**: depends on the clock, covered by unit tests
  (`EndOfDayWindowTests`), including daylight saving.
- **Not built, so nothing to test**: the officer's review of a referral, card ordering, 10-year retention.
