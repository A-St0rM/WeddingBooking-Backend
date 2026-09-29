using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Pricing;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Not "public": Supabase publishes that schema as a REST API reachable with
    /// the anon key, which ships in the browser. Everything of ours lives here,
    /// including EF's own history table.
    /// </summary>
    public const string Schema = "app";

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PricingSettings> PricingSettings => Set<PricingSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Every id is a Guid v7 made by the domain. Left to convention, EF treats a
        // new child found through a navigation (a contact added to a customer) as
        // an existing row, because its key is already set, and issues an UPDATE.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var key in entity.GetKeys().SelectMany(key => key.Properties).Where(p => p.ClrType == typeof(Guid)))
            {
                key.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }
    }
}
