using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Bookinger;
using WeddingBooking.Domain.Brugere;

namespace WeddingBooking.Infrastructure.Persistence;

public sealed class WeddingBookingDbContext(DbContextOptions<WeddingBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<Booking> Bookinger => Set<Booking>();
    public DbSet<Bruger> Brugere => Set<Bruger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Not "public": Supabase publishes that schema as a REST API that the
        // anon key can reach, and the anon key ships in the browser. Our tables
        // are reachable only through the .NET API, so they live where Supabase's
        // Data API cannot see them at all. See ADR-0007.
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeddingBookingDbContext).Assembly);
    }
}
