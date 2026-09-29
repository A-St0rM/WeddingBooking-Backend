using System.ComponentModel.DataAnnotations;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Payments;

namespace WeddingBooking.Api.Features.Payments;

public enum InstalmentState
{
    NotInvoiced,
    Draft,
    Booked,
    Paid,
    Overdue,
}

public sealed record DocumentResponse(
    string Number, DocumentType Type, DocumentStatus Status, decimal Amount, decimal Remaining, DateOnly? DueDate, DateTimeOffset CreatedAt)
{
    public static DocumentResponse From(AccountingDocument document) => new(
        document.Number, document.Type, document.Status, document.Amount, document.Remaining, document.DueDate, document.CreatedAt);
}

public sealed record InstalmentResponse(
    InstalmentKind Kind,
    decimal Amount,
    DateOnly DueDate,
    InstalmentState State,
    DocumentResponse? Invoice);

public sealed record PaymentsResponse(
    // Always true today. The UI shows "Simuleret regnskab" wherever this is.
    bool Simulated,
    PaymentTerms Terms,
    decimal Total,
    string? CustomerNumber,
    DocumentResponse? Quote,
    IReadOnlyList<InstalmentResponse> Instalments);

public sealed record CreateInvoiceRequest([Required] InstalmentKind Kind);
