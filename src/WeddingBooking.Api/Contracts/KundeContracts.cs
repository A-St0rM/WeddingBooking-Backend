namespace WeddingBooking.Api.Contracts;

public sealed record OpretKundeRequest(
    string Navn,
    string? Adresse,
    string? Postnr,
    string? By);

public sealed record KontaktpersonRequest(
    string Navn,
    string? Email,
    string? Telefon);

public sealed record KontaktpersonResponse(
    Guid Id,
    string Navn,
    string? Email,
    string? Telefon,
    bool ErPrimær);

public sealed record KundeResponse(
    Guid Id,
    string Navn,
    string? Adresse,
    string? Postnr,
    string? By,
    int? EconomicCustomerNumber,
    IReadOnlyList<KontaktpersonResponse> Kontaktpersoner);

/// <summary>A customer and everything they have ever booked here.</summary>
public sealed record KundeMedHistorikResponse(
    KundeResponse Kunde,
    IReadOnlyList<BookingResponse> Bookinger);
