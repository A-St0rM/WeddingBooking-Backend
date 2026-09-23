using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Contracts;
using WeddingBooking.Domain.Bookinger;
using WeddingBooking.Domain.Kunder;
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
    /// <param name="søg">Matches the couple's name — how a caller is found when they give no date.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Hent(
        [FromQuery] DateOnly? bryllupsdato,
        [FromQuery] string? søg,
        CancellationToken cancellationToken)
    {
        var query = Grundforespørgsel();

        if (bryllupsdato is not null)
        {
            query = query.Where(række => række.Booking.Bryllupsdato == bryllupsdato);
        }

        if (!string.IsNullOrWhiteSpace(søg))
        {
            var mønster = $"%{søg.Trim()}%";
            query = query.Where(række => EF.Functions.ILike(række.Kundenavn, mønster));
        }

        var bookinger = await query
            .OrderBy(række => række.Booking.Bryllupsdato == null)
            .ThenBy(række => række.Booking.Bryllupsdato)
            .ThenBy(række => række.Booking.Oprettet)
            .ToListAsync(cancellationToken);

        return Ok(bookinger.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> HentEn(Guid id, CancellationToken cancellationToken)
    {
        var række = await Grundforespørgsel()
            .FirstOrDefaultAsync(r => r.Booking.Id == id, cancellationToken);

        return række is null ? NotFound() : Ok(Map(række));
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

        var kunde = await FindEllerOpretKunde(request, cancellationToken);
        if (kunde is null)
        {
            ModelState.AddModelError(nameof(request.KundeId),
                "Angiv enten en eksisterende kunde eller et navn på en ny.");
            return ValidationProblem(ModelState);
        }

        var booking = Booking.Create(kunde.Id, request.Bryllupsdato, request.Gæsteantal);
        database.Bookinger.Add(booking);
        await database.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(HentEn), new { id = booking.Id },
            Mapning.TilResponse(booking, kunde.Navn, kunde.Navn));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Ret(
        Guid id, RetBookingRequest request, CancellationToken cancellationToken)
    {
        if (!Booking.ErGyldigtGæsteantal(request.Gæsteantal))
        {
            ModelState.AddModelError(nameof(request.Gæsteantal), Booking.UgyldigtGæsteantal);
            return ValidationProblem(ModelState);
        }

        var booking = await database.Bookinger.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking is null) return NotFound();

        booking.SætBryllupsdato(request.Bryllupsdato);
        booking.SætGæsteantal(request.Gæsteantal);
        await database.SaveChangesAsync(cancellationToken);

        return await HentEn(id, cancellationToken);
    }

    /// <summary>Parents sometimes pay. Null hands the bill back to the couple.</summary>
    [HttpPut("{id:guid}/betaler")]
    public async Task<ActionResult<BookingResponse>> SætBetaler(
        Guid id, SætBetalerRequest request, CancellationToken cancellationToken)
    {
        var booking = await database.Bookinger.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking is null) return NotFound();

        if (request.BetalerKundeId is null)
        {
            booking.NulstilBetaler();
        }
        else
        {
            var findes = await database.Kunder
                .AnyAsync(k => k.Id == request.BetalerKundeId, cancellationToken);
            if (!findes)
            {
                ModelState.AddModelError(nameof(request.BetalerKundeId), "Betaleren findes ikke.");
                return ValidationProblem(ModelState);
            }

            booking.SætBetaler(request.BetalerKundeId.Value);
        }

        await database.SaveChangesAsync(cancellationToken);
        return await HentEn(id, cancellationToken);
    }

    private async Task<Kunde?> FindEllerOpretKunde(
        OpretBookingRequest request, CancellationToken cancellationToken)
    {
        if (request.KundeId is not null)
        {
            return await database.Kunder
                .FirstOrDefaultAsync(kunde => kunde.Id == request.KundeId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.NytKundenavn))
        {
            return null;
        }

        var ny = Kunde.Create(request.NytKundenavn);
        database.Kunder.Add(ny);
        return ny;
    }

    private sealed record Række(Booking Booking, string Kundenavn, string Betalernavn);

    /// The payer's name is joined in too, so the list never has to guess whether
    /// "the customer" and "who gets the invoice" are the same person.
    private IQueryable<Række> Grundforespørgsel() =>
        from booking in database.Bookinger.AsNoTracking()
        join kunde in database.Kunder.AsNoTracking() on booking.KundeId equals kunde.Id
        join betaler in database.Kunder.AsNoTracking() on booking.BetalerKundeId equals betaler.Id
        select new Række(booking, kunde.Navn, betaler.Navn);

    private static BookingResponse Map(Række række) =>
        Mapning.TilResponse(række.Booking, række.Kundenavn, række.Betalernavn);
}
