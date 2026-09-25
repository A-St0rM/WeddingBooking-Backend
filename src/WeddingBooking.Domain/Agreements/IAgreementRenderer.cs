namespace WeddingBooking.Domain.Agreements;

/// <summary>Turns what was agreed into the PDF the couple signs.</summary>
public interface IAgreementRenderer
{
    byte[] Render(AgreementContent content);
}

/// <summary>
/// Everything the agreement says, taken from the booking — nothing is typed in
/// again. The clarification is Johan's own text: what "breakfast for all guests"
/// means in practice, what is left out for now.
/// </summary>
public sealed record AgreementContent(
    int Version,
    DateTimeOffset GeneratedAt,
    string CustomerName,
    string? Address,
    string? PostalCode,
    string? City,
    IReadOnlyList<AgreementContact> Contacts,
    DateOnly EventDate,
    int GuestCount,
    IReadOnlyList<AgreementLine> Lines,
    decimal? Surcharge,
    int SurchargeThreshold,
    decimal Total,
    string PaymentTermsText,
    IReadOnlyList<AgreementPayment> Payments,
    string? Clarification,
    bool IsPreview);

public sealed record AgreementContact(string Name, string? Email, string? Phone);

public sealed record AgreementLine(string Description, string Group, string Quantity, decimal UnitPrice, decimal Total);

public sealed record AgreementPayment(string Name, decimal Amount, DateOnly DueDate);
