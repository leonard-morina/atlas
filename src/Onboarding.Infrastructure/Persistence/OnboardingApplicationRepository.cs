using Atlas.Onboarding.Application.Persistence;
using Atlas.Onboarding.Domain.Applications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Onboarding.Infrastructure.Persistence;

internal sealed class OnboardingApplicationRepository(OnboardingDbContext db) : IOnboardingApplicationRepository
{
    public Task<OnboardingApplication?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Applications.AsNoTracking().SingleOrDefaultAsync(application => application.Id == id, cancellationToken);

    public Task<OnboardingApplication?> FindByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(application => application.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<bool> HasBlockingApplicationAsync(
        string market,
        ApplicantIdentifier identifier,
        CancellationToken cancellationToken) =>
        db.Applications.AnyAsync(
            application => application.Market == market
                && application.Status != ApplicationStatus.Rejected
                && application.Identifier.Type == identifier.Type
                && application.Identifier.Value == identifier.Value
                && application.Identifier.IssuingCountry == identifier.IssuingCountry,
            cancellationToken);

    public async Task<bool> TryAddAsync(OnboardingApplication application, CancellationToken cancellationToken)
    {
        db.Applications.Add(application);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: DuplicateKeyInUniqueIndex })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private const int DuplicateKeyInUniqueIndex = 2601;
}
