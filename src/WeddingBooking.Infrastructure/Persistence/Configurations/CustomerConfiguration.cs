using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Address).HasMaxLength(200);
        builder.Property(customer => customer.PostalCode).HasMaxLength(10);
        builder.Property(customer => customer.City).HasMaxLength(100);
        builder.Property(customer => customer.AccountingNumber).HasMaxLength(50);

        builder.HasMany(customer => customer.Contacts)
            .WithOne()
            .HasForeignKey(contact => contact.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(customer => customer.Contacts).HasField("_contacts").AutoInclude();
        builder.Ignore(customer => customer.Name);
        builder.Ignore(customer => customer.Partners);
        builder.Ignore(customer => customer.Payer);
    }
}

internal sealed class ContactPersonConfiguration : IEntityTypeConfiguration<ContactPerson>
{
    public void Configure(EntityTypeBuilder<ContactPerson> builder)
    {
        builder.ToTable("contact_persons");
        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(contact => contact.LastName).HasMaxLength(100);
        builder.Ignore(contact => contact.FullName);
        builder.Property(contact => contact.Email).HasMaxLength(320);
        builder.Property(contact => contact.Phone).HasMaxLength(40);
    }
}
