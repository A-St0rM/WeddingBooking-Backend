namespace WeddingBooking.Api.Contracts;

/// <remarks>
/// Every field is optional. A Booking is valid with partial information —
/// an enquiry taken over the phone rarely has all of it.
/// </remarks>
public sealed record OpretBookingRequest(
    string? Kundenavn,
    DateOnly? Bryllupsdato,
    int? Gæsteantal);

public sealed record BookingResponse(
    Guid Id,
    string? Kundenavn,
    DateOnly? Bryllupsdato,
    int? Gæsteantal,
    DateTimeOffset Oprettet);
