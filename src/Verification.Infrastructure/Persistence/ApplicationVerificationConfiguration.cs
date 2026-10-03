using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Verification.Domain;
using Atlas.Verification.Domain.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Atlas.Verification.Infrastructure.Persistence;

internal sealed class ApplicationVerificationConfiguration : IEntityTypeConfiguration<ApplicationVerification>
{
    public void Configure(EntityTypeBuilder<ApplicationVerification> builder)
    {
        builder.ToTable("Verifications");

        // One verification per application, enforced by the key itself.
        builder.HasKey(verification => verification.ApplicationId);
        builder.Property(verification => verification.ApplicationId).ValueGeneratedNever();

        builder.Property(verification => verification.Market).HasMaxLength(2).IsUnicode(false);
        builder.Property(verification => verification.Outcome).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        // A list of enum values, stored as a JSON array of their names in one column.
        builder.PrimitiveCollection(verification => verification.Reasons)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType(reason => reason.HasConversion<string>());

        builder.OwnsOne(verification => verification.Identity, identity =>
        {
            identity.Property(i => i.IdentificationId).HasColumnName("IdentificationId").HasMaxLength(64).IsUnicode(false);
            identity.Property(i => i.DocumentResult).HasColumnName("DocumentResult")
                .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            identity.Property(i => i.FaceMatch).HasColumnName("FaceMatch");
            identity.Property(i => i.Confidence).HasColumnName("Confidence");
        });

        builder.OwnsOne(verification => verification.Screening, screening =>
        {
            screening.Property(s => s.CaseId).HasColumnName("ScreeningCaseId").HasMaxLength(64).IsUnicode(false);
            screening.Property(s => s.Status).HasColumnName("ScreeningStatus")
                .HasConversion<string>().HasMaxLength(20).IsUnicode(false);
            // A JSON column through a converter rather than an owned collection, so EF can build the record
            // through its constructor.
            screening.Property(s => s.Matches).HasColumnName("ScreeningMatches")
                .HasConversion(MatchesConverter, MatchesComparer);
        });
    }

    private static readonly JsonSerializerOptions MatchesJson = new() { Converters = { new JsonStringEnumConverter() } };

    private static readonly ValueConverter<IReadOnlyList<ScreeningMatch>, string> MatchesConverter = new(
        matches => JsonSerializer.Serialize(matches, MatchesJson),
        json => JsonSerializer.Deserialize<List<ScreeningMatch>>(json, MatchesJson) ?? new List<ScreeningMatch>());

    private static readonly ValueComparer<IReadOnlyList<ScreeningMatch>> MatchesComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        matches => matches.Aggregate(0, (hash, match) => HashCode.Combine(hash, match)),
        matches => matches.ToList());
}
