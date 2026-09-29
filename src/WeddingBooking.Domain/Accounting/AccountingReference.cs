using WeddingBooking.Domain.Payments;

namespace WeddingBooking.Domain.Accounting;

/// <summary>
/// A number the accounting system gave back: which quote or invoice belongs to
/// the booking. Only the reference — never a status or an amount. Those belong
/// to the accounting system and are asked for every time.
/// </summary>
public sealed class AccountingReference
{
    private AccountingReference() { }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public DocumentType Type { get; private set; }
    public string Number { get; private set; } = string.Empty;

    /// <summary>Which instalment an invoice is for; null for a quote.</summary>
    public InstalmentKind? Instalment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static AccountingReference Create(Guid bookingId, DocumentType type, string number, InstalmentKind? instalment) => new()
    {
        Id = Guid.CreateVersion7(),
        BookingId = bookingId,
        Type = type,
        Number = number,
        Instalment = instalment,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
