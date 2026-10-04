using MediatR;

namespace Atlas.Onboarding.Application.Features.Applications.RecordAccountOpened;

/// <summary>Record the account core banking opened. Mapped from Accounts' AccountOpened by the consumer.</summary>
public sealed record RecordAccountOpenedCommand(Guid ApplicationId, string AccountNumber, DateTimeOffset OpenedAt) : IRequest;
