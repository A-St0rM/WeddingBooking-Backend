using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Kunder;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

public sealed class KundeConfiguration : IEntityTypeConfiguration<Kunde>
{
    public void Configure(EntityTypeBuilder<Kunde> builder)
    {
        builder.ToTable("kunder");

        builder.HasKey(kunde => kunde.Id);
        builder.Property(kunde => kunde.Id).HasColumnName("id");

        builder.Property(kunde => kunde.Navn)
            .HasColumnName("navn").HasMaxLength(200).IsRequired();

        builder.Property(kunde => kunde.Adresse).HasColumnName("adresse").HasMaxLength(300);
        builder.Property(kunde => kunde.Postnr).HasColumnName("postnr").HasMaxLength(20);
        builder.Property(kunde => kunde.By).HasColumnName("by").HasMaxLength(120);
        builder.Property(kunde => kunde.EconomicCustomerNumber).HasColumnName("economic_customer_number");
        builder.Property(kunde => kunde.Oprettet).HasColumnName("oprettet");

        // Customers are looked up by name constantly — it is how the event
        // manager finds a couple who rings without giving a date.
        builder.HasIndex(kunde => kunde.Navn);

        builder.HasMany(kunde => kunde.Kontaktpersoner)
            .WithOne()
            .HasForeignKey(kontakt => kontakt.KundeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(kunde => kunde.Kontaktpersoner)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
