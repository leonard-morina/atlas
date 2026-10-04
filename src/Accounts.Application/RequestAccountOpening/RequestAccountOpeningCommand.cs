using Atlas.Accounts.Domain.Openings;

namespace Atlas.Accounts.Application.RequestAccountOpening;

public sealed record RequestAccountOpeningCommand(Guid ApplicationId, string Market, Customer Customer);
