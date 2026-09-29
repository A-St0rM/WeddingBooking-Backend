using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Pricing;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class PricingSettingsConfiguration : IEntityTypeConfiguration<PricingSettings>
{
    public void Configure(EntityTypeBuilder<PricingSettings> builder)
    {
        builder.ToTable("pricing_settings", table => table.HasCheckConstraint("ck_pricing_settings_singleton", "id = 1"));
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.SmallWeddingSurcharge).HasPrecision(12, 2);
        builder.Ignore(settings => settings.Surcharge);
    }
}
