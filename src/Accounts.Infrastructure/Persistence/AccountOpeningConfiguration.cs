using Atlas.Accounts.Domain.Openings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Accounts.Infrastructure.Persistence;

internal sealed class AccountOpeningConfiguration : IEntityTypeConfiguration<AccountOpening>
{
    public const string Version = nameof(Version);

    public void Configure(EntityTypeBuilder<AccountOpening> builder)
    {
        builder.ToTable("AccountOpenings");

        // One opening per application, enforced by the key itself: an application gets one account, ever.
        builder.HasKey(opening => opening.ApplicationId);
        builder.Property(opening => opening.ApplicationId).ValueGeneratedNever();

        builder.Property(opening => opening.Market).HasMaxLength(2).IsUnicode(false);
        builder.Property(opening => opening.ChannelReference)
            .HasMaxLength(AccountOpening.ChannelReferenceLength).IsUnicode(false);
        builder.Property(opening => opening.Status).HasConversion<string>().HasMaxLength(24).IsUnicode(false);
        builder.Property(opening => opening.AccountNumber).HasMaxLength(34).IsUnicode(false);
        builder.Property(opening => opening.EscalationReason).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.Property(opening => opening.EscalationDetail).HasMaxLength(500);

        builder.OwnsOne(opening => opening.Customer, customer =>
        {
            customer.Property(c => c.FirstName).HasColumnName("FirstName").HasMaxLength(100);
            customer.Property(c => c.LastName).HasColumnName("LastName").HasMaxLength(100);
            customer.Property(c => c.DateOfBirth).HasColumnName("DateOfBirth");
            customer.Property(c => c.NationalId).HasColumnName("NationalId").HasMaxLength(20).IsUnicode(false);
            customer.Property(c => c.PassportNumber).HasColumnName("PassportNumber").HasMaxLength(9).IsUnicode(false);
            customer.Property(c => c.PassportIssuingCountry).HasColumnName("PassportIssuingCountry")
                .HasMaxLength(3).IsUnicode(false).IsFixedLength();
        });

        // Two workers changing the same opening (a call reporting after its lease was recovered): the second loses.
        builder.Property<byte[]>(Version).IsRowVersion();

        // The claim: a market's due openings, and its calls in flight.
        builder.HasIndex(opening => new { opening.Market, opening.Status, opening.DueAt });
    }
}
