using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Bookings;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", table =>
            table.HasCheckConstraint("ck_bookings_guest_count", $"guest_count BETWEEN 1 AND {Booking.MaxGuests}"));
        builder.HasKey(booking => booking.Id);

        builder.HasOne(booking => booking.Customer)
            .WithMany()
            .HasForeignKey(booking => booking.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(booking => booking.PaymentTerms).HasConversion<string>().HasMaxLength(10);
        builder.Property(booking => booking.Stage).HasConversion<string>().HasMaxLength(20);

        // A uint row version maps to Postgres' system column xmin: no column to add.
        builder.Property(booking => booking.Version).IsRowVersion();

        builder.HasIndex(booking => booking.EventDate);
        builder.HasIndex(booking => booking.CustomerId);
    }
}
