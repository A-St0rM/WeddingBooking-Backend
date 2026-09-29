using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeddingBooking.Integrations.Accounting.Simulated;

internal sealed class DesignTimeSimDbContextFactory : IDesignTimeDbContextFactory<AccountingSimDbContext>
{
    public AccountingSimDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountingSimDbContext>();
        IntegrationsExtensions.ConfigureSim(options,
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? "Host=localhost;Database=design_time;Username=postgres");
        return new AccountingSimDbContext(options.Options);
    }
}
