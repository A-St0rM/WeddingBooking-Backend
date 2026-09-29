using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Common;

namespace WeddingBooking.Integrations.Accounting.Simulated;

/// <summary>
/// An accounting system that behaves like e-conomic where it matters: it hands
/// out consecutive numbers, takes attachments only on drafts, sends invoices
/// only when they are booked, cannot send quotes, and never calls back — status
/// is asked for. This is production code: it is what the system runs on today.
/// </summary>
internal sealed class SimulatedAccountingSystem(AccountingSimDbContext db, TimeProvider clock) : IAccountingSystem
{
    public bool IsSimulated => true;

    public async Task<string> UpsertCustomerAsync(AccountingCustomer customer, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.Customers.FirstOrDefaultAsync(c => c.Reference == customer.Reference, ct);
        if (existing is null)
        {
            existing = new SimCustomer
            {
                Number = await NextAsync("customer_numbers", ct),
                Reference = customer.Reference,
                CreatedAt = now,
            };
            db.Customers.Add(existing);
        }

        existing.Name = customer.Name;
        existing.Address = customer.Address;
        existing.PostalCode = customer.PostalCode;
        existing.City = customer.City;
        existing.Email = customer.Email;
        existing.Phone = customer.Phone;
        existing.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return existing.Number.ToString();
    }

    public Task<AccountingDocument> CreateQuoteAsync(string customerNumber, DocumentDraft draft, CancellationToken ct) =>
        CreateAsync(DocumentType.Quote, "quote_numbers", customerNumber, draft, ct);

    public Task<AccountingDocument> CreateInvoiceAsync(string customerNumber, DocumentDraft draft, CancellationToken ct) =>
        CreateAsync(DocumentType.Invoice, "invoice_numbers", customerNumber, draft, ct);

    public async Task<AccountingDocument> BookInvoiceAsync(string invoiceNumber, CancellationToken ct)
    {
        var document = await FindAsync(invoiceNumber, ct);
        if (document.Type != DocumentType.Invoice) throw new DomainException("Kun en faktura kan bogføres.");
        if (document.Status != DocumentStatus.Draft) return ToPort(document);

        var now = clock.GetUtcNow();
        document.Status = DocumentStatus.Booked;
        document.BookedAt = now;
        document.SentAt = now; // e-conomic sends an invoice when it is booked, and only then.
        await db.SaveChangesAsync(ct);
        return ToPort(document);
    }

    public async Task AttachPdfAsync(string documentNumber, string fileName, byte[] pdf, CancellationToken ct)
    {
        var document = await FindAsync(documentNumber, ct);
        if (document.Status != DocumentStatus.Draft)
        {
            throw new DomainException("Regnskabet tager kun imod bilag, mens tilbuddet eller fakturaen er en kladde.");
        }

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw new DomainException("Regnskabet tager kun PDF som bilag.");

        db.Add(new SimAttachment { DocumentNumber = document.Number, FileName = fileName, Content = pdf, AttachedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccountingDocument>> GetDocumentsAsync(IReadOnlyCollection<string> documentNumbers, CancellationToken ct)
    {
        var numbers = documentNumbers.Select(n => int.TryParse(n, out var number) ? number : -1).Where(n => n > 0).ToList();
        if (numbers.Count == 0) return [];

        var documents = await db.Documents.AsNoTracking().Where(d => numbers.Contains(d.Number)).ToListAsync(ct);
        return documents.Select(ToPort).ToList();
    }

    public async Task<byte[]?> GetPdfAsync(string documentNumber, CancellationToken ct)
    {
        if (!int.TryParse(documentNumber, out var number)) return null;
        var document = await db.Documents.AsNoTracking().Include(d => d.Customer).FirstOrDefaultAsync(d => d.Number == number, ct);
        return document is null ? null : SimDocumentPdf.Render(document);
    }

    private async Task<AccountingDocument> CreateAsync(DocumentType type, string sequence, string customerNumber, DocumentDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Count == 0) throw new DomainException("Et tilbud eller en faktura skal have mindst én linje.");

        if (await db.Documents.FirstOrDefaultAsync(d => d.IdempotencyKey == draft.IdempotencyKey, ct) is { } existing)
        {
            return ToPort(existing);
        }

        var customer = int.TryParse(customerNumber, out var number)
            ? await db.Customers.FirstOrDefaultAsync(c => c.Number == number, ct)
            : null;
        if (customer is null) throw new DomainException($"Kunde {customerNumber} findes ikke i regnskabet.");

        var document = new SimDocument
        {
            Number = await NextAsync(sequence, ct),
            Type = type,
            CustomerNumber = customer.Number,
            Heading = draft.Heading,
            Status = DocumentStatus.Draft,
            IdempotencyKey = draft.IdempotencyKey,
            DueDate = draft.DueDate,
            CreatedAt = clock.GetUtcNow(),
            Lines = draft.Lines.Select((line, index) => new SimDocumentLine
            {
                SortOrder = index,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
            }).ToList(),
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
        return ToPort(document);
    }

    private async Task<SimDocument> FindAsync(string documentNumber, CancellationToken ct) =>
        (int.TryParse(documentNumber, out var number) ? await db.Documents.FirstOrDefaultAsync(d => d.Number == number, ct) : null)
        ?? throw new DomainException($"Bilag {documentNumber} findes ikke i regnskabet.");

    private Task<int> NextAsync(string sequence, CancellationToken ct)
    {
        // The sequence name goes in as a parameter and is resolved by Postgres, not spliced into the SQL.
        var qualified = $"{AccountingSimDbContext.Schema}.{sequence}";
        return db.Database.SqlQuery<int>($"SELECT nextval({qualified}::regclass)::int AS \"Value\"").SingleAsync(ct);
    }

    internal static AccountingDocument ToPort(SimDocument document) => new(
        document.Number.ToString(),
        document.Type,
        document.Status,
        document.Amount,
        document.Remaining,
        document.DueDate,
        document.CreatedAt);
}
