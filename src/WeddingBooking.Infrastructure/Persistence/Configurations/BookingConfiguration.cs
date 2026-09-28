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
        builder.Property(booking => booking.Origin).HasConversion<string>().HasMaxLength(20);

        // A uint row version maps to Postgres' system column xmin: no column to add.
        builder.Property(booking => booking.Version).IsRowVersion();

        builder.HasMany(booking => booking.Lines).WithOne().HasForeignKey(line => line.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(booking => booking.Lines).HasField("_lines").AutoInclude();
        builder.Ignore(booking => booking.OrderedLines);

        builder.HasMany(booking => booking.AccountingReferences).WithOne().HasForeignKey(r => r.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(booking => booking.AccountingReferences).HasField("_references").AutoInclude();
        builder.Ignore(booking => booking.LatestQuote);

        builder.HasMany(booking => booking.ReadinessItems).WithOne().HasForeignKey(item => item.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(booking => booking.ReadinessItems).HasField("_readiness").AutoInclude();
        builder.Ignore(booking => booking.BookedOn);
        builder.Ignore(booking => booking.SignedOn);

        builder.ComplexProperty(booking => booking.Surcharge, surcharge =>
        {
            surcharge.Property(rule => rule.Threshold).HasColumnName("surcharge_threshold");
            surcharge.Property(rule => rule.Amount).HasColumnName("surcharge_amount").HasPrecision(12, 2);
        });

        builder.HasOne<WeddingBooking.Domain.Users.User>().WithMany().HasForeignKey(booking => booking.ViewingHostId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(booking => booking.TrelloCardId).HasMaxLength(64);
        builder.HasIndex(booking => booking.TrelloCardId).IsUnique();

        builder.HasIndex(booking => booking.EventDate);
        builder.HasIndex(booking => booking.CustomerId);
    }
}

internal sealed class BookingLineConfiguration : IEntityTypeConfiguration<BookingLine>
{
    public void Configure(EntityTypeBuilder<BookingLine> builder)
    {
        builder.ToTable("booking_lines");
        builder.HasKey(line => line.Id);
        LineColumns.Configure(builder);
        builder.Property(line => line.SourcePackageName).HasMaxLength(200);
        builder.Ignore(line => line.Spec);
    }
}

internal sealed class AccountingReferenceConfiguration : IEntityTypeConfiguration<WeddingBooking.Domain.Accounting.AccountingReference>
{
    public void Configure(EntityTypeBuilder<WeddingBooking.Domain.Accounting.AccountingReference> builder)
    {
        builder.ToTable("accounting_references");
        builder.HasKey(reference => reference.Id);
        builder.Property(reference => reference.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(reference => reference.Instalment).HasConversion<string>().HasMaxLength(30);
        builder.Property(reference => reference.Number).HasMaxLength(50).IsRequired();

        builder.HasIndex(reference => reference.BookingId);

        // One invoice per instalment per booking, in the database as well as in the domain.
        builder.HasIndex(reference => new { reference.BookingId, reference.Instalment })
            .IsUnique()
            .HasFilter("type = 'Invoice'");
    }
}

internal sealed class ReadinessItemConfiguration : IEntityTypeConfiguration<WeddingBooking.Domain.Readiness.ReadinessItem>
{
    public void Configure(EntityTypeBuilder<WeddingBooking.Domain.Readiness.ReadinessItem> builder)
    {
        builder.ToTable("readiness_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Title).HasMaxLength(200);
        builder.Property(item => item.Responsible).HasMaxLength(200);
        builder.Property(item => item.UpdatedBy).HasMaxLength(200);
    }
}
