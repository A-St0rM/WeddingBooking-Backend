using WeddingBooking.Domain.Payments;

namespace WeddingBooking.Domain.Accounting;

/// <summary>
/// The accounting system, shaped by what the booking system needs rather than by
/// any one vendor's API. Today a simulated system sits behind it; e-conomic or
/// Dinero can be put in its place without the rest of the system changing.
/// </summary>
/// <remarks>
/// The accounting system owns the money. The booking system keeps only the
/// numbers it is given back — customer, quote and invoice numbers — and asks for
/// status every time. There are no callbacks: e-conomic has no webhooks.
/// </remarks>
public interface IAccountingSystem
{
    /// <summary>Whether this is the simulated system. The UI says so wherever it shows accounting data.</summary>
    bool IsSimulated { get; }

    /// <summary>Creates or updates the customer. Idempotent: the same reference always gives the same number.</summary>
    Task<string> UpsertCustomerAsync(AccountingCustomer customer, CancellationToken ct);

    /// <summary>Creates a quote as a draft. Quotes cannot be sent through the API; that happens in the accounting system.</summary>
    Task<AccountingDocument> CreateQuoteAsync(string customerNumber, DocumentDraft draft, CancellationToken ct);

    /// <summary>
    /// Creates an invoice as a draft. Idempotent on <see cref="DocumentDraft.IdempotencyKey"/>:
    /// asking twice for the same instalment gives the same invoice, not two.
    /// </summary>
    Task<AccountingDocument> CreateInvoiceAsync(string customerNumber, DocumentDraft draft, CancellationToken ct);

    /// <summary>Books an invoice draft, which also sends it. A booked invoice cannot be changed or have files attached.</summary>
    Task<AccountingDocument> BookInvoiceAsync(string invoiceNumber, CancellationToken ct);

    /// <summary>Attaches a PDF. Only possible while the document is a draft, as in e-conomic.</summary>
    Task AttachPdfAsync(string documentNumber, string fileName, byte[] pdf, CancellationToken ct);

    /// <summary>The current state of the documents asked for — status, amount and what remains to be paid.</summary>
    Task<IReadOnlyList<AccountingDocument>> GetDocumentsAsync(IReadOnlyCollection<string> documentNumbers, CancellationToken ct);

    Task<byte[]?> GetPdfAsync(string documentNumber, CancellationToken ct);
}

public sealed record AccountingCustomer(
    // Our own id for the customer; what makes the call idempotent.
    string Reference,
    string Name,
    string? Address,
    string? PostalCode,
    string? City,
    string? Email,
    string? Phone);

public sealed record DocumentLine(string Description, decimal Quantity, decimal UnitPrice);

public sealed record DocumentDraft(
    string IdempotencyKey,
    string Heading,
    IReadOnlyList<DocumentLine> Lines,
    DateOnly? DueDate,
    InstalmentKind? Instalment = null);

public enum DocumentType
{
    Quote,
    Invoice,
}

public enum DocumentStatus
{
    Draft,

    /// <summary>A quote sent to the customer from the accounting system.</summary>
    Sent,

    /// <summary>An invoice booked (bogført) and sent. Can no longer be changed.</summary>
    Booked,

    /// <summary>An invoice with nothing left to pay.</summary>
    Paid,
}

public sealed record AccountingDocument(
    string Number,
    DocumentType Type,
    DocumentStatus Status,
    decimal Amount,
    decimal Remaining,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt);
