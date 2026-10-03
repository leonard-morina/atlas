using Atlas.Verification.Domain.Checks;

namespace Atlas.Verification.Application.Abstractions;

/// <summary>Sanctions and PEP screening. Refinitiv World-Check in production; implemented by Infrastructure.</summary>
public interface ISanctionsScreening
{
    Task<ScreeningCheck> ScreenAsync(string name, DateOnly dateOfBirth, string nationality, CancellationToken cancellationToken);
}
