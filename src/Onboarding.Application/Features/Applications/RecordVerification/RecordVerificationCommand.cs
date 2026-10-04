using Atlas.Onboarding.Domain.Applications;
using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.RecordVerification;

/// <summary>Record what verification decided. Mapped from Verification's VerificationCompleted by the consumer.</summary>
public sealed record RecordVerificationCommand(
    Guid ApplicationId,
    VerificationVerdict Verdict,
    DateTimeOffset DecidedAt) : IRequest;
