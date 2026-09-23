namespace WeddingBooking.Api.Contracts;

/// <remarks>
/// Almost every field is optional. A Booking is valid with partial information —
/// an enquiry taken over the phone rarely has all of it. The exception is the
/// couple: give either an existing customer, or a name to create one from.
/// </remarks>
public sealed record OpretBookingRequest(
    Guid? KundeId,
    string? NytKundenavn,
    DateOnly? Bryllupsdato,
    int? Gæsteantal);

public sealed record RetBookingRequest(
    DateOnly? Bryllupsdato,
    int? Gæsteantal);

/// <param name="BetalerKundeId">Null hands the bill back to the couple.</param>
public sealed record SætBetalerRequest(Guid? BetalerKundeId);

public sealed record BookingResponse(
    Guid Id,
    Guid KundeId,
    string Kundenavn,
    Guid BetalerKundeId,
    string Betalernavn,
    bool HarSærskiltBetaler,
    DateOnly? Bryllupsdato,
    int? Gæsteantal,
    DateTimeOffset Oprettet);

public sealed record MigResponse(
    Guid Id,
    string Navn,
    string Email,
    string Rolle);
