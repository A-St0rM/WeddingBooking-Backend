using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Bookinger;
using WeddingBooking.Domain.Kunder;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

/// <remarks>
/// Column names are ASCII-folded snake_case (gaesteantal, not gæsteantal) for the
/// same reason file paths are, per ADR-0001: psql, migration tooling and shell
/// pipelines all handle æøå badly enough to be worth avoiding. The C# property
/// keeps its Danish spelling.
/// </remarks>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookinger");

        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).HasColumnName("id");

        builder.Property(booking => booking.KundeId).HasColumnName("kunde_id");
        builder.Property(booking => booking.BetalerKundeId).HasColumnName("betaler_kunde_id");

        builder.HasOne<Kunde>()
            .WithMany()
            .HasForeignKey(booking => booking.KundeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The payer is a Kunde too — usually the same one, sometimes the parents.
        builder.HasOne<Kunde>()
            .WithMany()
            .HasForeignKey(booking => booking.BetalerKundeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booking => booking.KundeId);

        builder.Property(booking => booking.Bryllupsdato)
            .HasColumnName("bryllupsdato");

        builder.Property(booking => booking.Gæsteantal)
            .HasColumnName("gaesteantal");

        builder.Property(booking => booking.Oprettet)
            .HasColumnName("oprettet");

        // The list is read soonest-wedding-first on every page load, and the
        // duplicate-date warning queries this column directly.
        builder.HasIndex(booking => booking.Bryllupsdato);
    }
}
