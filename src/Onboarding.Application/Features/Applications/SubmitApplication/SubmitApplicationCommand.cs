using Atlas.Onboarding.Domain.Applications;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.SubmitApplication;

/// <summary>
/// Submit an onboarding application. Independent of the HTTP contract: every API version maps its request
/// to this command, so the handler does not change when a new version is added. Values are already validated.
/// </summary>
public sealed record SubmitApplicationCommand(
    Guid IdempotencyKey,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Market,
    string Nationality,
    ApplicantIdentifier Identifier,
    string Email,
    string Phone,
    IReadOnlyList<ApplicantDocument> Documents) : IRequest<SubmitApplicationResult>;

/// <summary>An identity document or selfie, already decoded from the request's base64.</summary>
public sealed record ApplicantDocument(DocumentType Type, byte[] Content);
