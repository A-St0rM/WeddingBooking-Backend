using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WeddingBooking.Domain.Messages;

namespace WeddingBooking.Integrations.Mail.Simulated;

/// <summary>
/// "Sends" by putting the mail in an outbox table, where it can be read back.
/// Nothing leaves the building. A one.com SMTP adapter can take its place.
/// </summary>
internal sealed class SimulatedMessageSender(SimulatedMailDbContext db, TimeProvider clock) : IMessageSender
{
    public bool IsSimulated => true;

    public async Task<string> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        var entry = new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            FromName = message.FromName,
            ReplyTo = message.ReplyTo,
            To = string.Join(", ", message.To),
            Cc = string.Join(", ", message.Cc),
            Subject = message.Subject,
            Body = message.Body,
            SentAt = clock.GetUtcNow(),
        };
        db.Outbox.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id.ToString();
    }
}

/// <summary>What the simulated outbox holds, newest first.</summary>
public sealed class SimulatedOutbox(SimulatedMailDbContext db)
{
    public async Task<IReadOnlyList<OutboxMessage>> ListAsync(CancellationToken ct) =>
        await db.Outbox.AsNoTracking().OrderByDescending(m => m.SentAt).Take(200).ToListAsync(ct);
}

internal sealed class DesignTimeMailDbContextFactory : IDesignTimeDbContextFactory<SimulatedMailDbContext>
{
    public SimulatedMailDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SimulatedMailDbContext>();
        IntegrationsExtensions.ConfigureFor(options, SimulatedMailDbContext.Schema,
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? "Host=localhost;Database=design_time;Username=postgres");
        return new SimulatedMailDbContext(options.Options);
    }
}
