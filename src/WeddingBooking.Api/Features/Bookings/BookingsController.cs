using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(AppDbContext db, BookingResponder responder) : ControllerBase
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
            foreach (var pattern in Search.CouplePatterns(search))
            {
                query = query.Where(booking => booking.Customer.Contacts.Any(contact =>
                    EF.Functions.ILike(contact.FirstName, pattern) || EF.Functions.ILike(contact.LastName!, pattern)));
            }
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
        return await responder.SummariesAsync(bookings, ct);
    }

    /// <summary>Live bookings already on a date — for the warning while a date is being chosen.</summary>
    [HttpGet("on-date")]
    public async Task<IReadOnlyList<BookingSummaryResponse>> OnDate([FromQuery] DateOnly date, [FromQuery] Guid? except, CancellationToken ct) =>
        await responder.SummariesAsync(await db.OnDateAsync(date, except, ct), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken ct)
    {
        var booking = await db.WithCustomer().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        return await responder.RespondAsync(booking, ct);
    }

    /// <summary>
    /// A booking from a name, a date and a guest count. Give an existing customer's
    /// id to hang the booking on them — a couple who come back are the same customer.
    /// </summary>
    [HttpPost, ManagementOnly]
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
            if (request.Partners is not [var first, var second])
            {
                throw new DomainException("Skriv begge personers navn, eller vælg en eksisterende kunde.");
            }
            customer = Customer.Create(first.ToDetails(), second.ToDetails());
            db.Customers.Add(customer);
        }

        // The surcharge is copied onto the booking, like a price: changing it later leaves this booking alone.
        var settings = await db.PricingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var booking = Booking.Create(customer, request.EventDate, request.GuestCount, request.PaymentTerms ?? PaymentTerms.New,
            settings?.Surcharge);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = booking.Id }, await responder.RespondAsync(booking, ct));
    }

    [HttpPut("{id:guid}"), ManagementOnly]
    public async Task<ActionResult<BookingResponse>> Update(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        // Compare against the version the caller saw, not the one just loaded.
        db.Entry(booking).Property(b => b.Version).OriginalValue = request.Version;
        booking.Change(request.EventDate, request.GuestCount, request.PaymentTerms);
        await db.SaveChangesAsync(ct);

        return await responder.RespondAsync(booking, ct);
    }

    [HttpPut("{id:guid}/stage"), ManagementOnly]
    public async Task<ActionResult<BookingResponse>> ChangeStage(Guid id, ChangeStageRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        booking.MoveTo(request.Stage);
        await db.SaveChangesAsync(ct);

        return await responder.RespondAsync(booking, ct);
    }

    /// <summary>Books the viewing: when, and who shows the couple round.</summary>
    [HttpPut("{id:guid}/viewing"), ManagementOnly]
    public async Task<ActionResult<BookingResponse>> ScheduleViewing(Guid id, ScheduleViewingRequest request, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        if (request.HostId is { } hostId && !await db.Users.AnyAsync(u => u.Id == hostId && u.IsActive, ct))
        {
            throw new DomainException("Værten findes ikke blandt brugerne.");
        }

        booking.ScheduleViewing(request.At, request.HostId);
        await db.SaveChangesAsync(ct);
        return await responder.RespondAsync(booking, ct);
    }

    /// <summary>The staff who can show a couple round, for choosing a viewing's host.</summary>
    [HttpGet("viewing-hosts")]
    public async Task<IReadOnlyList<ViewingHostResponse>> ViewingHosts(CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name)
            .Select(u => new ViewingHostResponse(u.Id, u.Name, u.Phone)).ToListAsync(ct);

    /// <summary>Takes a request from the website on: the viewing is booked, or the date request becomes an inquiry.</summary>
    [HttpPost("{id:guid}/request/accept"), ManagementOnly]
    public async Task<ActionResult<BookingResponse>> AcceptRequest(Guid id, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        booking.AcceptRequest();
        await db.SaveChangesAsync(ct);
        return await responder.RespondAsync(booking, ct);
    }

    /// <summary>Says no to a request. Its time or date is freed, and why is kept in the history.</summary>
    [HttpPost("{id:guid}/request/decline"), ManagementOnly]
    public async Task<ActionResult<BookingResponse>> DeclineRequest(Guid id, DeclineRequest request, [FromServices] CurrentUser currentUser, [FromServices] TimeProvider clock, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        booking.DeclineRequest();
        var user = currentUser.User;
        db.Notes.Add(Domain.Notes.Note.Write(booking.Id, Domain.Notes.NoteType.Internal,
            string.IsNullOrWhiteSpace(request.Reason) ? "Bestillingen fra hjemmesiden er afvist." : $"Bestillingen fra hjemmesiden er afvist: {request.Reason.Trim()}",
            user.Id, user.Name, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        return await responder.RespondAsync(booking, ct);
    }
}
