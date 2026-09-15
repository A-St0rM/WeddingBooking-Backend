using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Tests.Fixtures;

/// <summary>
/// Boots the real application in memory and calls it over real HTTP. This is the
/// primary seam: no service is replaced, no repository is mocked. What the tests
/// exercise is what runs in production.
/// </summary>
/// <remarks>
/// Nothing here touches the database. Migrating in <c>InitializeAsync</c> would
/// make an unreachable database fail every test in the class, including the ones
/// that never query it — which is how a network problem comes to look like a
/// broken validation rule. Only <see cref="ResetAsync"/> connects, and only the
/// tests that need data call it.
/// </remarks>
public sealed class ApiFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? factory;
    private bool migrated;

    public HttpClient Client { get; private set; } = default!;

    public Task InitializeAsync()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            if (TestDatabase.IsConfigured)
            {
                builder.UseSetting("ConnectionStrings:Postgres", TestDatabase.ConnectionString);
            }
        });

        Client = factory.CreateClient();
        return Task.CompletedTask;
    }

    /// Each test starts from an empty table; tests must not depend on each other.
    public async Task ResetAsync()
    {
        if (factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WeddingBookingDbContext>();

        if (!migrated)
        {
            await database.Database.MigrateAsync();
            migrated = true;
        }

        await database.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE {PersistenceRegistration.Schema}.bookinger");
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        factory?.Dispose();
        return Task.CompletedTask;
    }
}
