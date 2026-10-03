using Atlas.Onboarding.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Onboarding.Infrastructure.Persistence;

internal sealed class OnboardingApplicationConfiguration : IEntityTypeConfiguration<OnboardingApplication>
{
    public const string IdempotencyKeyIndex = "UX_Applications_IdempotencyKey";
    public const string BlockingApplicationIndex = "UX_Applications_BlockingIdentity";

    public void Configure(EntityTypeBuilder<OnboardingApplication> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).ValueGeneratedNever();

        builder.Property(application => application.RequestFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.Property(application => application.Market).HasMaxLength(2).IsUnicode(false);
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        builder.OwnsOne(application => application.Applicant, applicant =>
        {
            applicant.Property(a => a.FirstName).HasColumnName("FirstName").HasMaxLength(100);
            applicant.Property(a => a.LastName).HasColumnName("LastName").HasMaxLength(100);
            applicant.Property(a => a.DateOfBirth).HasColumnName("DateOfBirth");
            applicant.Property(a => a.Nationality).HasColumnName("Nationality").HasMaxLength(3).IsUnicode(false);
            applicant.Property(a => a.Email).HasColumnName("Email").HasMaxLength(254);
            applicant.Property(a => a.Phone).HasColumnName("Phone").HasMaxLength(16).IsUnicode(false);
        });

        builder.OwnsOne(application => application.Identifier, identifier =>
        {
            identifier.Property(i => i.Type).HasColumnName("IdentifierType")
                .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            // Leading zeros are significant (CDS-ID-04 §1.1): always a string.
            identifier.Property(i => i.Value).HasColumnName("IdentifierValue").HasMaxLength(13).IsUnicode(false);
            identifier.Property(i => i.IssuingCountry).HasColumnName("IdentifierIssuingCountry")
                .HasMaxLength(3).IsUnicode(false);
        });

        // Where each image is stored and its hash; the bytes themselves are in blob storage.
        builder.OwnsMany(application => application.Documents, document =>
        {
            document.ToTable("ApplicationDocuments");
            document.WithOwner().HasForeignKey("ApplicationId");
            document.HasKey("ApplicationId", nameof(ApplicationDocument.Type));
            document.Property(d => d.Type).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            document.Property(d => d.BlobName).HasMaxLength(100).IsUnicode(false);
            document.Property(d => d.Sha256).HasMaxLength(64).IsUnicode(false);
        });
        builder.Navigation(application => application.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The "submitted twice" guarantee. A unique index rather than a check in code: with several replicas,
        // two identical requests can both pass a check before either is stored.
        builder.HasIndex(application => application.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName(IdempotencyKeyIndex);

        // One blocking application per identifier and market, enforced by the database for the same reason.
        // The identifier columns belong to the owned type, so the index is over a computed column of them.
        builder.Property<string>("BlockingIdentity")
            .HasComputedColumnSql(
                "CONCAT([Market], '|', [IdentifierType], '|', [IdentifierIssuingCountry], '|', [IdentifierValue])",
                stored: true)
            .HasMaxLength(60)
            .IsUnicode(false);

        builder.HasIndex("BlockingIdentity")
            .IsUnique()
            .HasFilter($"[Status] <> '{nameof(ApplicationStatus.Rejected)}'")
            .HasDatabaseName(BlockingApplicationIndex);
    }
}
