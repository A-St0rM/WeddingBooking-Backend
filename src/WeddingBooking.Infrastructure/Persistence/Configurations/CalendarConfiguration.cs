using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Calendar;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class HostAvailabilityConfiguration : IEntityTypeConfiguration<HostAvailability>
{
    public void Configure(EntityTypeBuilder<HostAvailability> builder)
    {
        builder.ToTable("host_availability");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Day).HasConversion<string>().HasMaxLength(10);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.HostId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CalendarBlockConfiguration : IEntityTypeConfiguration<CalendarBlock>
{
    public void Configure(EntityTypeBuilder<CalendarBlock> builder)
    {
        builder.ToTable("calendar_blocks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Reason).HasMaxLength(200);
        builder.Ignore(b => b.Interval);
        builder.HasIndex(b => b.Start);
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.HostId).OnDelete(DeleteBehavior.Cascade);
    }
}
