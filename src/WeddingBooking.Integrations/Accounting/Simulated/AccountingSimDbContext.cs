using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Accounting;

namespace WeddingBooking.Integrations.Accounting.Simulated;

/// <summary>
/// The simulated accounting system's storage, in its own schema. The booking
/// system's tables hold only the numbers this hands out.
/// </summary>
public sealed class AccountingSimDbContext(DbContextOptions<AccountingSimDbContext> options) : DbContext(options)
{
    public const string Schema = "accounting_sim";

    internal DbSet<SimCustomer> Customers => Set<SimCustomer>();
    internal DbSet<SimDocument> Documents => Set<SimDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Numbering like a real ledger: consecutive, one series per kind, never reused.
        modelBuilder.HasSequence<int>("customer_numbers").StartsAt(1001);
        modelBuilder.HasSequence<int>("quote_numbers").StartsAt(5001);
        modelBuilder.HasSequence<int>("invoice_numbers").StartsAt(20001);

        modelBuilder.Entity<SimCustomer>(customer =>
        {
            customer.ToTable("customers");
            customer.HasKey(c => c.Number);
            customer.Property(c => c.Number).ValueGeneratedNever();
            customer.HasIndex(c => c.Reference).IsUnique();
            customer.Property(c => c.Reference).HasMaxLength(100);
            customer.Property(c => c.Name).HasMaxLength(200);
            customer.Property(c => c.Address).HasMaxLength(200);
            customer.Property(c => c.PostalCode).HasMaxLength(10);
            customer.Property(c => c.City).HasMaxLength(100);
            customer.Property(c => c.Email).HasMaxLength(320);
            customer.Property(c => c.Phone).HasMaxLength(40);
        });

        modelBuilder.Entity<SimDocument>(document =>
        {
            document.ToTable("documents");
            document.HasKey(d => d.Number);
            document.Property(d => d.Number).ValueGeneratedNever();
            document.Property(d => d.Type).HasConversion<string>().HasMaxLength(20);
            document.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            document.Property(d => d.Heading).HasMaxLength(300);
            document.Property(d => d.IdempotencyKey).HasMaxLength(200);
            document.HasIndex(d => d.IdempotencyKey).IsUnique();
            document.HasOne(d => d.Customer).WithMany().HasForeignKey(d => d.CustomerNumber).OnDelete(DeleteBehavior.Restrict);
            document.Ignore(d => d.Amount);
            document.Ignore(d => d.Paid);
            document.Ignore(d => d.Remaining);

            document.HasMany(d => d.Lines).WithOne().HasForeignKey(l => l.DocumentNumber).OnDelete(DeleteBehavior.Cascade);
            document.HasMany(d => d.Payments).WithOne().HasForeignKey(p => p.DocumentNumber).OnDelete(DeleteBehavior.Cascade);
            document.HasMany(d => d.Attachments).WithOne().HasForeignKey(a => a.DocumentNumber).OnDelete(DeleteBehavior.Cascade);
            document.Navigation(d => d.Lines).AutoInclude();
            document.Navigation(d => d.Payments).AutoInclude();
        });

        modelBuilder.Entity<SimDocumentLine>(line =>
        {
            line.ToTable("document_lines");
            line.Property(l => l.Description).HasMaxLength(500);
            line.Property(l => l.Quantity).HasPrecision(12, 2);
            line.Property(l => l.UnitPrice).HasPrecision(12, 2);
        });

        modelBuilder.Entity<SimPayment>(payment =>
        {
            payment.ToTable("payments");
            payment.Property(p => p.Amount).HasPrecision(12, 2);
            payment.Property(p => p.RegisteredBy).HasMaxLength(200);
        });

        modelBuilder.Entity<SimAttachment>(attachment =>
        {
            attachment.ToTable("attachments");
            attachment.Property(a => a.FileName).HasMaxLength(255);
        });
    }
}
