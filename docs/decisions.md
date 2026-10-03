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
