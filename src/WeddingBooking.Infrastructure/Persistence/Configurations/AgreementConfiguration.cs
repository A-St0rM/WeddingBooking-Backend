using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Agreements;
using WeddingBooking.Infrastructure.Files;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class AgreementVersionConfiguration : IEntityTypeConfiguration<AgreementVersion>
{
    public void Configure(EntityTypeBuilder<AgreementVersion> builder)
    {
        builder.ToTable("agreement_versions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.GeneratedBy).HasMaxLength(200);
        builder.Property(version => version.SignedUploadedBy).HasMaxLength(200);
        builder.HasIndex(version => new { version.BookingId, version.Number }).IsUnique();
        builder.HasOne<Domain.Bookings.Booking>().WithMany().HasForeignKey(version => version.BookingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FileRecord>().WithMany().HasForeignKey(version => version.PdfFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileRecord>().WithMany().HasForeignKey(version => version.SignedFileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> builder)
    {
        builder.ToTable("stored_files");
        builder.HasKey(file => file.Id);
        builder.Property(file => file.FileName).HasMaxLength(255);
        builder.Property(file => file.ContentType).HasMaxLength(100);
    }
}
