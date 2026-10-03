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
(the manual review that Compliance §3 requires), and they should not share a surface. Edge concerns
(request size limits for the document images, rate limiting) belong in one place. YARP runs as a normal
.NET project with `dotnet run`; Nginx/Envoy would need images outside the allowed list, and in Azure the
AKS ingress or API Management could take over this role.
