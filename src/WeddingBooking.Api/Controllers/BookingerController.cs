using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Contracts;
using WeddingBooking.Domain.Bookinger;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Controllers;

[ApiController]
[Route("api/bookinger")]
public sealed class BookingerController(WeddingBookingDbContext database) : ControllerBase
{
    /// <summary>
    /// Every Booking, soonest wedding first. Bookings with no date yet sort last:
    /// an enquiry without a date is not more urgent than next month's wedding.
    /// </summary>
    /// <param name="bryllupsdato">
    /// When given, returns only Bookings on that date. The create form calls this
    /// to warn before two weddings land on the same Saturday.
    /// </param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Hent(
        [FromQuery] DateOnly? bryllupsdato,
        CancellationToken cancellationToken)
    {
        var query = database.Bookinger.AsNoTracking();

        if (bryllupsdato is not null)
        {
            query = query.Where(booking => booking.Bryllupsdato == bryllupsdato);
        }

        var bookinger = await query
            .OrderBy(booking => booking.Bryllupsdato == null)
            .ThenBy(booking => booking.Bryllupsdato)
            .ThenBy(booking => booking.Oprettet)
            .Select(booking => Map(booking))
            .ToListAsync(cancellationToken);

        return Ok(bookinger);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> HentEn(Guid id, CancellationToken cancellationToken)
    {
        var booking = await database.Bookinger
            .AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);

        return booking is null ? NotFound() : Ok(Map(booking));
    }

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Opret(
        OpretBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!Booking.ErGyldigtGæsteantal(request.Gæsteantal))
        {
            ModelState.AddModelError(nameof(request.Gæsteantal), Booking.UgyldigtGæsteantal);
            return ValidationProblem(ModelState);
        }

        var booking = Booking.Create(request.Kundenavn, request.Bryllupsdato, request.Gæsteantal);

        database.Bookinger.Add(booking);
        await database.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(HentEn), new { id = booking.Id }, Map(booking));
    }

    private static BookingResponse Map(Booking booking) => new(
        booking.Id,
        booking.Kundenavn,
        booking.Bryllupsdato,
        booking.Gæsteantal,
        booking.Oprettet);
}
