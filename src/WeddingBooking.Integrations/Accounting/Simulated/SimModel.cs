using WeddingBooking.Domain.Accounting;

namespace WeddingBooking.Integrations.Accounting.Simulated;

// The simulated accounting system's own records. Nothing outside this folder
// reads them; the booking system sees them only through IAccountingSystem.

internal sealed class SimCustomer
{
    public int Number { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SimDocument
{
    public int Number { get; set; }
    public DocumentType Type { get; set; }
    public int CustomerNumber { get; set; }
    public SimCustomer Customer { get; set; } = null!;
    public string Heading { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }

    /// <summary>What makes creating an invoice for an instalment safe to repeat.</summary>
    public string? IdempotencyKey { get; set; }

    public DateOnly? DueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? BookedAt { get; set; }

    public List<SimDocumentLine> Lines { get; set; } = [];
    public List<SimPayment> Payments { get; set; } = [];
    public List<SimAttachment> Attachments { get; set; } = [];

    public decimal Amount => Lines.Sum(line => line.Quantity * line.UnitPrice);
    public decimal Paid => Payments.Sum(payment => payment.Amount);
    public decimal Remaining => Type == DocumentType.Invoice ? Amount - Paid : 0;
}

internal sealed class SimDocumentLine
{
    public int Id { get; set; }
    public int DocumentNumber { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

internal sealed class SimPayment
{
    public int Id { get; set; }
    public int DocumentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public string RegisteredBy { get; set; } = string.Empty;
}

internal sealed class SimAttachment
{
    public int Id { get; set; }
    public int DocumentNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public DateTimeOffset AttachedAt { get; set; }
}
