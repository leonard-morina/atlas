# Known limitations

What doesn't work, what I didn't get to, and what I'd do next (the order is in the design note).

## Doesn't work (yet)

- **Referrals never finish.** A possible match stays `REFERRED`, there's no way for an officer's decision to come back
  (out of scope in the ticket, sketched in docs/decisions.md).
- **Failed account openings are only an event.** When Accounts gives up it publishes `AccountOpeningEscalated`, nobody
  consumes it, and the customer stays `APPROVED`.
- **A provider down for more than about a minute** leaves the application `PROCESSING`: the message is retried a few
  times (5, 15, 30 s) and then parked in the error queue, someone has to move it back. Longer, delayed retries need a
  scheduler (a RabbitMQ plugin, built into Azure Service Bus).
- **A provider timeout isn't retried by the HTTP client** (a second call could be a second paid identification), the
  message retry calls both providers again though.
- **No card ordering**, no push notifications, no save-and-resume beyond retrying the same submission.

## Compliance requirements not met

- **Data residency (§1):** only the images are separated per market (by path). Database rows, messages and logs are
  in one place. Needs Compliance's answer first (open questions).
- **Access logging and no shared credentials (§2):** locally everything uses `sa`, there's no access log. Production
  would use one identity per service, and log reads of personal data.
- **Retention and erasure (§4, §5):** nothing deletes anything, and nothing enforces ten years either. Images left
  behind by a failed save aren't cleaned up (a storage lifecycle policy would).

## Simplifications

- **Anonymous API.** No authentication, so the rate limit is per IP address. People behind the same mobile network
  share one, so the limit is generous and easy to get around.
- **ME identifiers** are only checked for format, the check digit algorithm isn't published. **No minimum age** check.
- **Market time zones** for core banking's night window are assumed.
- **Migrations run on startup**, in Development only. There's no production path for them yet (decisions.md has the
  options), and no Dockerfiles or Kubernetes manifests.
- **The stand-ins keep their state in memory** and run as one instance. Restarting them forgets the accounts they
  opened.
- **A missing `Idempotency-Key`** gives a `400` with the framework's own wording ("Required parameter … was not
  provided from header"), not a friendlier message.

## Tooling and environment

- **Tested on macOS and Linux** (GitHub Actions), **not on Windows**.
- The integration tests use the same local containers as development (separate databases and RabbitMQ virtual host).
- The AppHost logs two harmless `crit` lines at startup (Aspire's orchestrator), and the dashboard's Stop button gives
  up after ~12 s while a worker may still be finishing a call.
- **MassTransit 8.5 and MediatR 12.5 are pinned**, the newer versions need a commercial licence.
- Opting one HTTP client out of the default retries uses an experimental API (`RemoveAllResilienceHandlers`), the
  warning is suppressed with a comment.
