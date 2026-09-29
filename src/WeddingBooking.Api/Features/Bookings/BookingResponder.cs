using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

/// <summary>
/// Turns bookings into the answer the caller may see. Everything derived — the
/// price, the status, the same-date warning — is worked out here, on every read.
/// Price and payment facts are left out for the roles that do not see prices;
/// hiding them in the browser would not be access control.
/// </summary>
public sealed class BookingResponder(AppDbContext db, CurrentUser currentUser, BookingFinance finance, TimeProvider clock)
{
    private bool SeesPrices => currentUser.User.Role.SeesPrices();

    public async Task<BookingResponse> RespondAsync(Booking booking, CancellationToken ct)
    {
        var sameDate = await SummariesAsync(await db.OnDateAsync(booking.EventDate, booking.Id, ct), ct);
        var payments = SeesPrices ? await finance.ForAsync(booking, ct) : null;
        var host = booking.ViewingHostId is { } hostId
            ? await db.Users.AsNoTracking().Where(u => u.Id == hostId).Select(u => new ViewingHostResponse(u.Id, u.Name, u.Phone)).FirstOrDefaultAsync(ct)
            : null;

        return new BookingResponse(
            booking.Id,
            booking.EventDate,
            booking.GuestCount,
            booking.PaymentTerms,
            booking.Stage,
            booking.Origin,
            booking.Kind,
            StatusOf(booking, payments),
            booking.SignedAt,
            booking.ViewingAt,
            host,
            booking.CreatedAt,
            booking.UpdatedAt,
            booking.Version,
            CustomerResponse.From(booking.Customer),
            booking.OrderedLines.Select(BookingLineResponse.From).ToList(),
            SeesPrices ? PriceOf(booking) : null,
            sameDate);
    }

    public async Task<IReadOnlyList<BookingSummaryResponse>> SummariesAsync(IReadOnlyCollection<Booking> bookings, CancellationToken ct)
    {
        var payments = SeesPrices && bookings.Count > 0 ? await finance.ForManyAsync(bookings, ct) : null;
        return bookings
            .Select(booking => BookingSummaryResponse.From(booking, StatusOf(booking, payments?.GetValueOrDefault(booking.Id))))
            .ToList();
    }

    private DisplayStatus StatusOf(Booking booking, PaymentsResponse? payments) =>
        BookingStatus.Derive(new StatusFacts(
            booking.Stage,
            booking.EventDate,
            Today.In(clock),
            ReservationFeePaid: payments?.ReservationFeePaid() ?? false,
            AnyInstalmentOverdue: payments?.AnyOverdue() ?? false,
            AllReadinessDone: ReadinessRules.AllDone(booking.ReadinessItems),
            PostEventOutstanding: ReadinessRules.PostEventOutstanding(booking.ReadinessItems)));

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
