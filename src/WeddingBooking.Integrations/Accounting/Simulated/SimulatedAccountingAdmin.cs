using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Common;

namespace WeddingBooking.Integrations.Accounting.Simulated;

public sealed record SimDocumentSummary(
    string Number,
    DocumentType Type,
    DocumentStatus Status,
    string CustomerNumber,
    string CustomerName,
    string Heading,
    decimal Amount,
    decimal Paid,
    decimal Remaining,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    int AttachmentCount);

/// <summary>
/// What someone would do in the accounting system's own screens: mark a quote
/// sent, register a payment from the bank. Not part of the port — the booking
/// system never does these. They exist so the whole flow can be shown to the end.
/// </summary>
public sealed class SimulatedAccountingAdmin(AccountingSimDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<SimDocumentSummary>> ListAsync(CancellationToken ct)
    {
        var documents = await db.Documents.AsNoTracking()
            .Include(d => d.Customer)
            .OrderByDescending(d => d.CreatedAt)
            .Take(300)
            .Select(d => new { Document = d, Attachments = d.Attachments.Count })
            .ToListAsync(ct);

        return documents.Select(row => Summary(row.Document, row.Attachments)).ToList();
    }

    /// <summary>A quote sent to the customer from the accounting system's own screen — the API cannot do it.</summary>
    public async Task MarkQuoteSentAsync(string number, CancellationToken ct)
    {
        var document = await FindAsync(number, ct);
        if (document.Type != DocumentType.Quote) throw new DomainException("Kun et tilbud markeres som sendt her. En faktura sendes, når den bogføres.");
        if (document.Status != DocumentStatus.Draft) return;

        document.Status = DocumentStatus.Sent;
        document.SentAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A payment seen in the bank, registered against a booked invoice.</summary>
    public async Task RegisterPaymentAsync(string number, decimal amount, DateOnly paidOn, string registeredBy, CancellationToken ct)
    {
        var document = await FindAsync(number, ct);
        if (document.Type != DocumentType.Invoice) throw new DomainException("Betalinger registreres på fakturaer.");
        if (document.Status == DocumentStatus.Draft) throw new DomainException("Fakturaen skal bogføres, før der kan registreres en betaling.");
        if (amount <= 0) throw new DomainException("Beløbet skal være større end 0.");
        if (amount > document.Remaining) throw new DomainException($"Der mangler kun {document.Remaining:N2} kr. på fakturaen.");

        document.Payments.Add(new SimPayment
        {
            DocumentNumber = document.Number,
            Amount = amount,
            PaidOn = paidOn,
            RegisteredAt = clock.GetUtcNow(),
            RegisteredBy = registeredBy,
        });
        if (document.Remaining == 0) document.Status = DocumentStatus.Paid;
        await db.SaveChangesAsync(ct);
    }

    private async Task<SimDocument> FindAsync(string number, CancellationToken ct) =>
        (int.TryParse(number, out var n) ? await db.Documents.FirstOrDefaultAsync(d => d.Number == n, ct) : null)
        ?? throw new DomainException($"Bilag {number} findes ikke i regnskabet.");

    private static SimDocumentSummary Summary(SimDocument document, int attachments) => new(
        document.Number.ToString(),
        document.Type,
        document.Status,
        document.CustomerNumber.ToString(),
        document.Customer.Name,
        document.Heading,
        document.Amount,
        document.Paid,
        document.Remaining,
        document.DueDate,
        document.CreatedAt,
        attachments);
}
