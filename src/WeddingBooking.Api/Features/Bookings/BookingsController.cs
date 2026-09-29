using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Common;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(AppDbContext db) : ControllerBase
{
    /// <summary>Bookings, soonest first. Past and closed bookings only when asked for.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<BookingSummaryResponse>> List(
        [FromQuery] string? search, [FromQuery] bool includePast = false, [FromQuery] bool includeClosed = false,
        CancellationToken ct = default)
    {
        var query = db.WithCustomer().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Search.Contains(search);
            query = query.Where(booking =>
                EF.Functions.ILike(booking.Customer.Name, pattern)
                || booking.Customer.Contacts.Any(contact => EF.Functions.ILike(contact.Name, pattern)));
        }

        if (!includePast)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(booking => booking.EventDate >= today);
        }

        if (!includeClosed)
        {
            query = query.Where(booking => booking.Stage != SalesStage.Cancelled && booking.Stage != SalesStage.Lost);
        }

        var bookings = await query.OrderBy(booking => booking.EventDate).Take(500).ToListAsync(ct);
        return bookings.Select(BookingSummaryResponse.From).ToList();
    }

    /// <summary>Live bookings already on a date — for the warning while a date is being chosen.</summary>
    [HttpGet("on-date")]
    public async Task<IReadOnlyList<BookingSummaryResponse>> OnDate([FromQuery] DateOnly date, [FromQuery] Guid? except, CancellationToken ct) =>
        (await db.OnDateAsync(date, except, ct)).Select(BookingSummaryResponse.From).ToList();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken ct)
    {
        var booking = await db.WithCustomer().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        return await ResponseAsync(booking, ct);
    }

    /// <summary>
    /// A booking from a name, a date and a guest count. Give an existing customer's
    /// id to hang the booking on them — a couple who come back are the same customer.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        Customer customer;
        if (request.CustomerId is { } customerId)
        {
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
            if (existing is null) return NotFound();
            customer = existing;
        }
        else
        {
            customer = Customer.Create(request.CustomerName ?? throw new DomainException("Skriv parrets navn, eller vælg en eksisterende kunde."));
            db.Customers.Add(customer);
        }

        var booking = Booking.Create(customer, request.EventDate, request.GuestCount, request.PaymentTerms ?? PaymentTerms.New);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = booking.Id }, await ResponseAsync(booking, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Update(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        // Compare against the version the caller saw, not the one just loaded.
        db.Entry(booking).Property(b => b.Version).OriginalValue = request.Version;
        booking.Change(request.EventDate, request.GuestCount, request.PaymentTerms);
        await db.SaveChangesAsync(ct);

        return await ResponseAsync(booking, ct);
    }

    [HttpPut("{id:guid}/stage")]
    public async Task<ActionResult<BookingResponse>> ChangeStage(Guid id, ChangeStageRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        booking.MoveTo(request.Stage);
        await db.SaveChangesAsync(ct);

        return await ResponseAsync(booking, ct);
    }

    private async Task<BookingResponse> ResponseAsync(Booking booking, CancellationToken ct)
    {
        var sameDate = await db.OnDateAsync(booking.EventDate, booking.Id, ct);
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
            sameDate.Select(BookingSummaryResponse.From).ToList());
    }
}
