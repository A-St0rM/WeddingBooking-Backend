using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Kunder;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

public sealed class KontaktpersonConfiguration : IEntityTypeConfiguration<Kontaktperson>
{
    public void Configure(EntityTypeBuilder<Kontaktperson> builder)
    {
        builder.ToTable("kontaktpersoner");

        builder.HasKey(kontakt => kontakt.Id);
        builder.Property(kontakt => kontakt.Id).HasColumnName("id");
        builder.Property(kontakt => kontakt.KundeId).HasColumnName("kunde_id");
        builder.Property(kontakt => kontakt.Navn).HasColumnName("navn").HasMaxLength(200).IsRequired();
        builder.Property(kontakt => kontakt.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(kontakt => kontakt.Telefon).HasColumnName("telefon").HasMaxLength(40);
        builder.Property(kontakt => kontakt.ErPrimær).HasColumnName("er_primaer");

        builder.HasIndex(kontakt => kontakt.KundeId);
    }
}
