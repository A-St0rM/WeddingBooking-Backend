using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Messages;
using WeddingBooking.Integrations.Accounting.Simulated;
using WeddingBooking.Integrations.Mail.Simulated;

namespace WeddingBooking.Integrations;

public static class IntegrationsExtensions
{
    /// <summary>
    /// Registers an adapter behind every port. Which one is chosen by
    /// configuration; today each port has only its simulated or offline adapter
    /// unless a key says otherwise.
    /// </summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        var connectionString = configuration.GetConnectionString("Postgres");
        services.AddDbContext<AccountingSimDbContext>(options => ConfigureSim(options, connectionString));
        services.AddScoped<IAccountingSystem, SimulatedAccountingSystem>();
        services.AddScoped<SimulatedAccountingAdmin>();
        services.AddSingleton<Domain.Agreements.IAgreementRenderer, Agreements.QuestPdfAgreementRenderer>();

        services.AddDbContext<SimulatedMailDbContext>(options => ConfigureFor(options, SimulatedMailDbContext.Schema, connectionString));
        services.AddScoped<IMessageSender, SimulatedMessageSender>();
        services.AddScoped<SimulatedOutbox>();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return services;
    }

    internal static void ConfigureSim(DbContextOptionsBuilder options, string? connectionString) =>
        ConfigureFor(options, AccountingSimDbContext.Schema, connectionString);

    /// <summary>Each simulated system keeps its tables and its migration history in its own schema.</summary>
    internal static void ConfigureFor(DbContextOptionsBuilder options, string schema, string? connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention();
}
