using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Brugere;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

public sealed class BrugerConfiguration : IEntityTypeConfiguration<Bruger>
{
    public void Configure(EntityTypeBuilder<Bruger> builder)
    {
        builder.ToTable("brugere");

        builder.HasKey(bruger => bruger.Id);
        builder.Property(bruger => bruger.Id).HasColumnName("id");

        builder.Property(bruger => bruger.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        // One person, one login. Email is how a Bruger and a Supabase account
        // find each other the first time.
        builder.HasIndex(bruger => bruger.Email).IsUnique();

        builder.Property(bruger => bruger.Navn)
            .HasColumnName("navn")
            .HasMaxLength(200)
            .IsRequired();

        // Stored as text, not an integer: a role is read straight out of the
        // database often enough that the number would be a nuisance.
        builder.Property(bruger => bruger.Rolle)
            .HasColumnName("rolle")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(bruger => bruger.AuthBrugerId).HasColumnName("auth_bruger_id");
        builder.HasIndex(bruger => bruger.AuthBrugerId);

        builder.Property(bruger => bruger.Oprettet).HasColumnName("oprettet");
        builder.Property(bruger => bruger.SidsteLogin).HasColumnName("sidste_login");
    }
}
