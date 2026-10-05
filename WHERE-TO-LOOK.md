# Where to look

**Read these first**

1. **[docs/design-note.md](docs/design-note.md)**: what I built, what not, and the order I'd do the rest in.
2. **Account opening**: `src/Accounts.Domain/Openings/AccountOpening.cs` (the state machine) and
   `src/Accounts.Application/ProcessOpenings/CallCoreBankingHandler.cs` (what each core banking answer means).
   Core banking has no idempotency and a timeout doesn't mean it failed, so this is where a mistake costs the most.
3. **The integration tests**: `tests/Atlas.IntegrationTests`, start with `SubmittedTwiceTests.cs` (same request five
   times at once, one application) and `CoreBankingTimeoutTests.cs` (a timeout that still ends in exactly one account).
   They run the real system against the real SQL Server and RabbitMQ.
4. **[docs/qa-notes.md](docs/qa-notes.md)** and **[docs/decisions.md](docs/decisions.md)**: the scenarios, and why
   things are the way they are.

**The decision I most want to be asked about**

That Accounts never calls OpenAccount a second time when it doesn't know what happened to the first. It looks the
account up by our own reference instead, and when it can't (passport customers, or the account never shows up) it
hands it to operations. So a customer can end up waiting for a person, but never with two accounts.

**What you can skip**

- `src/Stubs`: just stand-ins for IDNow, World-Check and core banking so it runs locally.
- API versioning: deliberately ahead of need, there's only v1 (see docs/decisions.md).
- The two `crit` log lines when the AppHost starts: Aspire's orchestrator, harmless.
