using WeddingBooking.Api.Common;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Payments;

namespace WeddingBooking.Api.Features.Payments;

/// <summary>
/// A booking's payment plan with the accounting system's view of each
/// instalment, read fresh through the port every time. Nothing here is stored.
/// </summary>
public sealed class BookingFinance(IAccountingSystem accounting, TimeProvider clock)
{
    public async Task<PaymentsResponse> ForAsync(Booking booking, CancellationToken ct)
    {
        var documents = await DocumentsAsync([booking], ct);
        return Build(booking, documents, Today.In(clock));
    }

    /// <summary>Status facts for many bookings in one round trip to the accounting system.</summary>
    public async Task<IReadOnlyDictionary<Guid, PaymentsResponse>> ForManyAsync(IReadOnlyCollection<Booking> bookings, CancellationToken ct)
    {
        var documents = await DocumentsAsync(bookings, ct);
        var today = Today.In(clock);
        return bookings.ToDictionary(booking => booking.Id, booking => Build(booking, documents, today));
    }

    private async Task<IReadOnlyDictionary<string, AccountingDocument>> DocumentsAsync(IEnumerable<Booking> bookings, CancellationToken ct)
    {
        var numbers = bookings.SelectMany(booking => booking.AccountingReferences).Select(reference => reference.Number).Distinct().ToList();
        if (numbers.Count == 0) return new Dictionary<string, AccountingDocument>();
        var documents = await accounting.GetDocumentsAsync(numbers, ct);
        return documents.ToDictionary(document => document.Number);
    }

    private PaymentsResponse Build(Booking booking, IReadOnlyDictionary<string, AccountingDocument> documents, DateOnly today)
    {
        AccountingDocument? Document(AccountingReference? reference) =>
            reference is not null && documents.TryGetValue(reference.Number, out var document) ? document : null;

        var invoices = Enum.GetValues<InstalmentKind>()
            .Select(kind => (Kind: kind, Invoice: Document(booking.InvoiceFor(kind))))
            .Where(pair => pair.Invoice is not null)
            .ToDictionary(pair => pair.Kind, pair => pair.Invoice!);

        var total = booking.Price().Total;
        var schedule = PaymentSchedule.For(
            booking.PaymentTerms, total, booking.EventDate, booking.BookedOn, booking.SignedOn,
            invoices.ToDictionary(pair => pair.Key, pair => pair.Value.Amount));

        var instalments = schedule.Select(payment =>
        {
            invoices.TryGetValue(payment.Kind, out var invoice);
            return new InstalmentResponse(
                payment.Kind,
                payment.Amount,
                invoice?.DueDate ?? payment.DueDate,
                StateOf(invoice, today),
                invoice is null ? null : DocumentResponse.From(invoice));
        }).ToList();

        var quote = Document(booking.LatestQuote);
        return new PaymentsResponse(
            accounting.IsSimulated,
            booking.PaymentTerms,
            total,
            booking.Customer.AccountingNumber,
            quote is null ? null : DocumentResponse.From(quote),
            instalments);
    }

    private static InstalmentState StateOf(AccountingDocument? invoice, DateOnly today) => invoice switch
    {
        null => InstalmentState.NotInvoiced,
        { Status: DocumentStatus.Paid } => InstalmentState.Paid,
        { Status: DocumentStatus.Draft } => InstalmentState.Draft,
        { DueDate: { } due } when due < today => InstalmentState.Overdue,
        _ => InstalmentState.Booked,
    };
}

public static class PaymentFacts
{
    public static bool ReservationFeePaid(this PaymentsResponse payments) =>
        payments.Instalments.Any(i => i.Kind == InstalmentKind.ReservationFee && i.State == InstalmentState.Paid);

    public static bool AnyOverdue(this PaymentsResponse payments) =>
        payments.Instalments.Any(i => i.State == InstalmentState.Overdue);
}
