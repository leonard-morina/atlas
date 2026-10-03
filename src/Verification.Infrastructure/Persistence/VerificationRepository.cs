using Atlas.Verification.Application.Abstractions;
using Atlas.Verification.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Verification.Infrastructure.Persistence;

internal sealed class VerificationRepository(VerificationDbContext db) : IVerificationRepository
{
    public Task<bool> ExistsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        db.Verifications.AnyAsync(verification => verification.ApplicationId == applicationId, cancellationToken);

    public void Add(ApplicationVerification verification) => db.Verifications.Add(verification);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
