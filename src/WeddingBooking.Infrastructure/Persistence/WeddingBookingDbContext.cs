using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Bookinger;

namespace WeddingBooking.Infrastructure.Persistence;

public sealed class WeddingBookingDbContext(DbContextOptions<WeddingBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<Booking> Bookinger => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeddingBookingDbContext).Assembly);
}
