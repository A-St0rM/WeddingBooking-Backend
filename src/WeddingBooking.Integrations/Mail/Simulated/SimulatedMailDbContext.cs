using Microsoft.EntityFrameworkCore;

namespace WeddingBooking.Integrations.Mail.Simulated;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string? ReplyTo { get; set; }
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
}

/// <summary>The simulated outbox, in its own schema like the simulated accounting system.</summary>
public sealed class SimulatedMailDbContext(DbContextOptions<SimulatedMailDbContext> options) : DbContext(options)
{
    public const string Schema = "mail_sim";

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<OutboxMessage>(message =>
        {
            message.ToTable("outbox");
            message.HasKey(m => m.Id);
            message.Property(m => m.Id).ValueGeneratedNever();
            message.Property(m => m.FromName).HasMaxLength(200);
            message.Property(m => m.ReplyTo).HasMaxLength(320);
            message.Property(m => m.To).HasMaxLength(2000);
            message.Property(m => m.Cc).HasMaxLength(2000);
            message.Property(m => m.Subject).HasMaxLength(500);
            message.HasIndex(m => m.SentAt);
        });
    }
}
