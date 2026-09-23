using WeddingBooking.Api.Contracts;
using WeddingBooking.Domain.Bookinger;
using WeddingBooking.Domain.Kunder;

namespace WeddingBooking.Api.Controllers;

/// <summary>
/// Domain objects to what the browser sees. Kept in one place so two
/// controllers cannot drift into describing the same Booking differently.
/// </summary>
internal static class Mapning
{
    public static BookingResponse TilResponse(Booking booking, string kundenavn, string betalernavn) => new(
        booking.Id,
        booking.KundeId,
        kundenavn,
        booking.BetalerKundeId,
        betalernavn,
        booking.HarSærskiltBetaler,
        booking.Bryllupsdato,
        booking.Gæsteantal,
        booking.Oprettet);

    public static KundeResponse TilResponse(Kunde kunde) => new(
        kunde.Id,
        kunde.Navn,
        kunde.Adresse,
        kunde.Postnr,
        kunde.By,
        kunde.EconomicCustomerNumber,
        [.. kunde.Kontaktpersoner
            .OrderByDescending(kontakt => kontakt.ErPrimær)
            .ThenBy(kontakt => kontakt.Navn)
            .Select(kontakt => new KontaktpersonResponse(
                kontakt.Id, kontakt.Navn, kontakt.Email, kontakt.Telefon, kontakt.ErPrimær))]);
}
