using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
