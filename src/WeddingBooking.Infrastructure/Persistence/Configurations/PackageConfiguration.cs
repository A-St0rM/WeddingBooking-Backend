using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Catalogue;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("packages");
        builder.HasKey(package => package.Id);
        builder.Property(package => package.Name).HasMaxLength(200).IsRequired();
        builder.Property(package => package.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(package => package.Description).HasMaxLength(2000);
        builder.HasIndex(package => package.Year);

        builder.HasMany(package => package.Lines).WithOne().HasForeignKey(line => line.PackageId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(package => package.Lines).HasField("_lines").AutoInclude();
    }
}

internal sealed class PackageLineConfiguration : IEntityTypeConfiguration<PackageLine>
{
    public void Configure(EntityTypeBuilder<PackageLine> builder)
    {
        builder.ToTable("package_lines");
        builder.HasKey(line => line.Id);
        LineColumns.Configure(builder);
        builder.Ignore(line => line.Spec);
    }
}

/// <summary>A package line and a booking line are the same shape, stored the same way.</summary>
internal static class LineColumns
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<string>("Description").HasMaxLength(500).IsRequired();
        builder.Property<ProductGroup>("Group").HasColumnName("product_group").HasConversion<string>().HasMaxLength(30);
        builder.Property<LinePricing>("Pricing").HasConversion<string>().HasMaxLength(20);
        builder.Property<decimal>("UnitPrice").HasPrecision(12, 2);
    }
}
