using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WeddingBooking.Domain.Users;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Accounting.Simulated;

namespace WeddingBooking.Api.Tests;

/// <summary>Runs only when a throwaway database is given: WB_TEST_POSTGRES=&lt;connection string&gt;.</summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(ApiFactory.ConnectionString)) Skip = "WB_TEST_POSTGRES er ikke sat.";
    }
}

/// <summary>
/// The real API against a real Postgres. Only the token issuer is swapped: tokens
/// are signed with a test key instead of by Supabase. The database is wiped and
/// migrated from scratch, so point it only at a throwaway database.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string? ConnectionString = Environment.GetEnvironmentVariable("WB_TEST_POSTGRES");
    public const string ManagerEmail = "manager@test.dk";

    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("test-signing-key-that-is-long-enough-for-hs256"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
        builder.UseSetting("Supabase:Url", "");
        builder.UseSetting("Access:BootstrapAdminEmails:0", ManagerEmail);
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = Key,
                    ValidIssuer = "test",
                    ValidAudience = "authenticated",
                };
            }));
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;
        using var scope = Services.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await app.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS app CASCADE; DROP SCHEMA IF EXISTS accounting_sim CASCADE; DROP SCHEMA IF EXISTS mail_sim CASCADE;");
        await app.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AccountingSimDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<WeddingBooking.Integrations.Mail.Simulated.SimulatedMailDbContext>().Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>A client signed in as someone with that e-mail — known to the system or not.</summary>
    public HttpClient ClientFor(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(email));
        return client;
    }

    public static string TokenFor(string email, TimeSpan? lifetime = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "test",
            Audience = "authenticated",
            Subject = new ClaimsIdentity([new Claim("sub", DeterministicId(email).ToString()), new Claim("email", email)]),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });

    /// <summary>Adds a member of staff directly, as the user admin would.</summary>
    public async Task<HttpClient> StaffAsync(string email, Role role)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Users.AnyAsync(u => u.Email == email))
        {
            db.Users.Add(User.Create(email, email.Split('@')[0], role));
            await db.SaveChangesAsync();
        }
        return ClientFor(email);
    }

    public static Guid DeterministicId(string email) =>
        new(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(email)));
}
