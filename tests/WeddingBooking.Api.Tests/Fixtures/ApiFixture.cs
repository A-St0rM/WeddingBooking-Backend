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
public sealed class ApiFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? factory;

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
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

        if (!TestDatabase.IsConfigured)
        {
            return;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WeddingBookingDbContext>();
        await database.Database.MigrateAsync();
    }

    /// Each test starts from an empty table; tests must not depend on each other.
    public async Task ResetAsync()
    {
        if (!TestDatabase.IsConfigured || factory is null)
        {
            return;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WeddingBookingDbContext>();
        await database.Database.ExecuteSqlRawAsync("TRUNCATE TABLE bookinger");
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        factory?.Dispose();
        return Task.CompletedTask;
    }
}
