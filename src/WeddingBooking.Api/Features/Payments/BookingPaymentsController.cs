using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Payments;
using WeddingBooking.Domain.Users;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Payments;

/// <summary>
/// The payment plan and the accounting system behind it. The booking keeps only
/// the numbers; amounts and status are read through the port on every call.
/// </summary>
[ApiController]
[Route("api/bookings/{id:guid}/payments")]
[PriceRolesOnly]
public sealed class BookingPaymentsController(AppDbContext db, IAccountingSystem accounting, BookingFinance finance) : ControllerBase
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    [HttpGet]
    public async Task<ActionResult<PaymentsResponse>> Get(Guid id, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        return booking is null ? NotFound() : await finance.ForAsync(booking, ct);
    }

    /// <summary>
    /// Creates the quote in the accounting system from the booking's lines — the
    /// customer is created or updated from the booking first, so nothing is typed
    /// twice. The quote is a draft; it is sent from the accounting system.
    /// </summary>
    [HttpPost("quote"), ManagementOnly]
    public async Task<ActionResult<PaymentsResponse>> CreateQuote(Guid id, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null) return NotFound();

        var price = booking.Price();
        if (price.Lines.Count == 0) throw new DomainException("Læg en pakke eller en linje på bookingen, før der laves et tilbud.");

        var lines = booking.OrderedLines.Zip(price.Lines, (line, priced) =>
                new DocumentLine(line.Pricing == Domain.Catalogue.LinePricing.PerGuest ? $"{line.Description} (pr. gæst)" : line.Description, priced.Units, priced.UnitPrice))
            .ToList();
        if (price.Surcharge > 0)
        {
            lines.Add(new DocumentLine($"Tillæg for bryllup under {booking.Surcharge.Threshold} gæster", 1, price.Surcharge));
        }

        var customerNumber = await UpsertCustomerAsync(booking, ct);
        var quote = await accounting.CreateQuoteAsync(customerNumber,
            new DocumentDraft($"{booking.Id}:quote:{Guid.CreateVersion7()}", Heading(booking, "Tilbud på bryllup"), lines, null), ct);

        booking.RecordQuote(quote.Number);
        await db.SaveChangesAsync(ct);
        return await finance.ForAsync(booking, ct);
    }

    /// <summary>
    /// Invoices one instalment for the amount the plan says now. Safe to repeat:
    /// the accounting system returns the same invoice for the same instalment.
    /// </summary>
    [HttpPost("invoices"), AllowRoles(Role.EventManager, Role.Owner, Role.Bookkeeper)]
    public async Task<ActionResult<PaymentsResponse>> CreateInvoice(Guid id, CreateInvoiceRequest request, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null) return NotFound();
        if (booking.Stage.IsClosed()) throw new DomainException("Der faktureres ikke på en afbestilt eller tabt booking.");

        var plan = await finance.ForAsync(booking, ct);
        var instalment = plan.Instalments.FirstOrDefault(i => i.Kind == request.Kind)
            ?? throw new DomainException("Raten findes ikke under bookingens betalingsbetingelser.");
        if (instalment.Invoice is not null) return plan;
        if (instalment.Amount <= 0) throw new DomainException("Raten er på 0 kr. og faktureres ikke.");

        var customerNumber = await UpsertCustomerAsync(booking, ct);
        var name = InstalmentName(request.Kind, booking);
        var invoice = await accounting.CreateInvoiceAsync(customerNumber,
            new DocumentDraft(
                $"{booking.Id}:{request.Kind}",
                Heading(booking, name),
                [new DocumentLine(name, 1, instalment.Amount)],
                instalment.DueDate,
                request.Kind), ct);

        booking.RecordInvoice(request.Kind, invoice.Number);
        await db.SaveChangesAsync(ct);
        return await finance.ForAsync(booking, ct);
    }

    /// <summary>Books the invoice draft, which sends it. It cannot be changed afterwards.</summary>
    [HttpPost("invoices/{number}/book"), AllowRoles(Role.EventManager, Role.Owner, Role.Bookkeeper)]
    public async Task<ActionResult<PaymentsResponse>> BookInvoice(Guid id, string number, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null || !Owns(booking, number)) return NotFound();

        await accounting.BookInvoiceAsync(number, ct);
        return await finance.ForAsync(booking, ct);
    }

    [HttpGet("documents/{number}/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Pdf(Guid id, string number, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null || !Owns(booking, number)) return NotFound();

        var pdf = await accounting.GetPdfAsync(number, ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"bilag-{number}.pdf");
    }

    private Task<Booking?> LoadAsync(Guid id, CancellationToken ct) =>
        db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);

    private static bool Owns(Booking booking, string number) =>
        booking.AccountingReferences.Any(reference => reference.Number == number);

    private async Task<string> UpsertCustomerAsync(Booking booking, CancellationToken ct)
    {
        var customer = booking.Customer;
        // The payer is the accounting system's customer: their name and the address go on the quote and the invoice.
        var payer = customer.Payer ?? throw new DomainException("Vælg, hvem af de to der betaler, før der laves tilbud eller faktura.");
        var number = await accounting.UpsertCustomerAsync(new AccountingCustomer(
            customer.Id.ToString(), payer.FullName, customer.Address, customer.PostalCode, customer.City, payer.Email, payer.Phone), ct);
        customer.RecordAccountingNumber(number);
        return number;
    }

    private static string Heading(Booking booking, string what) =>
        $"{what} — {booking.Customer.Name}, {booking.EventDate.ToString("d. MMMM yyyy", Danish)}";

    private static string InstalmentName(InstalmentKind kind, Booking booking) => kind switch
    {
        InstalmentKind.ReservationFee => "Reservationsgebyr (modregnes i restbetalingen)",
        InstalmentKind.Instalment => "Rate: 33 % af brylluppets pris",
        _ => booking.PaymentTerms == PaymentTerms.New ? "Restbetaling" : "Betaling af brylluppet, fratrukket reservationsgebyr",
    };
}
