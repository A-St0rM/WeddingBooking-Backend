using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

/// <summary>
/// Turns a booking into the answer the caller may see. Everything derived — the
/// price, the same-date warning — is worked out here, on every read. Price fields
/// are left out for the roles that do not see prices; hiding them in the browser
/// would not be access control.
/// </summary>
public sealed class BookingResponder(AppDbContext db, CurrentUser currentUser)
{
    public async Task<BookingResponse> RespondAsync(Booking booking, CancellationToken ct)
    {
        var sameDate = await db.OnDateAsync(booking.EventDate, booking.Id, ct);
        var lines = booking.OrderedLines.ToList();

        return new BookingResponse(
            booking.Id,
            booking.EventDate,
            booking.GuestCount,
            booking.PaymentTerms,
            booking.Stage,
            booking.CreatedAt,
            booking.UpdatedAt,
            booking.Version,
            CustomerResponse.From(booking.Customer),
            lines.Select(BookingLineResponse.From).ToList(),
            currentUser.User.Role.SeesPrices() ? PriceOf(booking) : null,
            sameDate.Select(BookingSummaryResponse.From).ToList());
    }

    private static PriceResponse PriceOf(Booking booking)
    {
        var price = booking.Price();
        return new PriceResponse(
            price.Lines.Select(line => new PricedLineResponse(line.LineId, line.UnitPrice, line.Units, line.Total)).ToList(),
            price.Subtotal,
            new SurchargeResponse(booking.Surcharge.Threshold, booking.Surcharge.Amount, price.Surcharge > 0),
            price.Total);
    }
}
