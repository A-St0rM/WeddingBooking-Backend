using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Bookinger;

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

        builder.Property(booking => booking.Kundenavn)
            .HasColumnName("kundenavn")
            .HasMaxLength(200);

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
