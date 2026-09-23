using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Contracts;
using WeddingBooking.Domain.Kunder;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Controllers;

[ApiController]
[Route("api/kunder")]
public sealed class KunderController(WeddingBookingDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KundeResponse>>> Hent(
        [FromQuery] string? søg, CancellationToken cancellationToken)
    {
        var query = database.Kunder.AsNoTracking().Include(kunde => kunde.Kontaktpersoner).AsQueryable();

        if (!string.IsNullOrWhiteSpace(søg))
        {
            var mønster = $"%{søg.Trim()}%";
            query = query.Where(kunde => EF.Functions.ILike(kunde.Navn, mønster));
        }

        var kunder = await query.OrderBy(kunde => kunde.Navn).ToListAsync(cancellationToken);
        return Ok(kunder.Select(Mapning.TilResponse).ToList());
    }

    /// <summary>
    /// A customer and everything they have ever booked here — the reason a Kunde
    /// outlives its Bookings rather than being recreated for each one.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KundeMedHistorikResponse>> HentEn(
        Guid id, CancellationToken cancellationToken)
    {
        var kunde = await database.Kunder.AsNoTracking()
            .Include(k => k.Kontaktpersoner)
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

        if (kunde is null) return NotFound();

        var historik = await (
            from booking in database.Bookinger.AsNoTracking()
            join ejer in database.Kunder.AsNoTracking() on booking.KundeId equals ejer.Id
            join betaler in database.Kunder.AsNoTracking() on booking.BetalerKundeId equals betaler.Id
            where booking.KundeId == id || booking.BetalerKundeId == id
            orderby booking.Bryllupsdato descending
            select Mapning.TilResponse(booking, ejer.Navn, betaler.Navn)
        ).ToListAsync(cancellationToken);

        return Ok(new KundeMedHistorikResponse(Mapning.TilResponse(kunde), historik));
    }

    [HttpPost]
    public async Task<ActionResult<KundeResponse>> Opret(
        OpretKundeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Navn))
        {
            ModelState.AddModelError(nameof(request.Navn), "En kunde skal have et navn.");
            return ValidationProblem(ModelState);
        }

        var kunde = Kunde.Create(request.Navn, request.Adresse, request.Postnr, request.By);
        database.Kunder.Add(kunde);
        await database.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(HentEn), new { id = kunde.Id }, Mapning.TilResponse(kunde));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KundeResponse>> Ret(
        Guid id, OpretKundeRequest request, CancellationToken cancellationToken)
    {
        var kunde = await Spor(id, cancellationToken);
        if (kunde is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Navn))
        {
            ModelState.AddModelError(nameof(request.Navn), "En kunde skal have et navn.");
            return ValidationProblem(ModelState);
        }

        kunde.Ret(request.Navn, request.Adresse, request.Postnr, request.By);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(Mapning.TilResponse(kunde));
    }

    [HttpPost("{id:guid}/kontaktpersoner")]
    public async Task<ActionResult<KundeResponse>> TilføjKontaktperson(
        Guid id, KontaktpersonRequest request, CancellationToken cancellationToken)
    {
        var kunde = await Spor(id, cancellationToken);
        if (kunde is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Navn))
        {
            ModelState.AddModelError(nameof(request.Navn), "En kontaktperson skal have et navn.");
            return ValidationProblem(ModelState);
        }

        kunde.TilføjKontaktperson(request.Navn, request.Email, request.Telefon);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(Mapning.TilResponse(kunde));
    }

    [HttpPut("{id:guid}/kontaktpersoner/{kontaktpersonId:guid}")]
    public async Task<ActionResult<KundeResponse>> RetKontaktperson(
        Guid id, Guid kontaktpersonId, KontaktpersonRequest request, CancellationToken cancellationToken)
    {
        var kunde = await Spor(id, cancellationToken);
        if (kunde is null) return NotFound();

        var kontakt = kunde.Kontaktpersoner.FirstOrDefault(k => k.Id == kontaktpersonId);
        if (kontakt is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Navn))
        {
            ModelState.AddModelError(nameof(request.Navn), "En kontaktperson skal have et navn.");
            return ValidationProblem(ModelState);
        }

        kontakt.Ret(request.Navn, request.Email, request.Telefon);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(Mapning.TilResponse(kunde));
    }

    /// <summary>Promoting one contact demotes the other; there is exactly one default recipient.</summary>
    [HttpPut("{id:guid}/kontaktpersoner/{kontaktpersonId:guid}/primaer")]
    public async Task<ActionResult<KundeResponse>> GørPrimær(
        Guid id, Guid kontaktpersonId, CancellationToken cancellationToken)
    {
        var kunde = await Spor(id, cancellationToken);
        if (kunde is null) return NotFound();

        if (kunde.Kontaktpersoner.All(k => k.Id != kontaktpersonId)) return NotFound();

        kunde.GørPrimær(kontaktpersonId);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(Mapning.TilResponse(kunde));
    }

    [HttpDelete("{id:guid}/kontaktpersoner/{kontaktpersonId:guid}")]
    public async Task<ActionResult<KundeResponse>> FjernKontaktperson(
        Guid id, Guid kontaktpersonId, CancellationToken cancellationToken)
    {
        var kunde = await Spor(id, cancellationToken);
        if (kunde is null) return NotFound();

        kunde.FjernKontaktperson(kontaktpersonId);
        await database.SaveChangesAsync(cancellationToken);
        return Ok(Mapning.TilResponse(kunde));
    }

    private Task<Kunde?> Spor(Guid id, CancellationToken cancellationToken) =>
        database.Kunder.Include(kunde => kunde.Kontaktpersoner)
            .FirstOrDefaultAsync(kunde => kunde.Id == id, cancellationToken);
}
