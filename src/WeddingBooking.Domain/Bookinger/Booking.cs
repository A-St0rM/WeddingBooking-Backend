namespace WeddingBooking.Domain.Bookinger;

/// <summary>
/// One wedding, from first enquiry to final invoice. See CONTEXT.md.
/// </summary>
/// <remarks>
/// A Booking is valid with partial information: a couple who ring with a date
/// but no firm guest count still get recorded. Nothing here is rejected for
/// being incomplete — except the Kunde, because a wedding without anyone
/// getting married is not an enquiry, it is a mistake.
/// </remarks>
public sealed class Booking
{
    private Booking() { }

    public Guid Id { get; private set; }

    /// <summary>The couple. A Kunde outlives its Bookings.</summary>
    public Guid KundeId { get; private set; }

    /// <summary>
    /// Who is liable for payment, and therefore who gets invoiced. Defaults to
    /// the Kunde; parents sometimes pay.
    /// </summary>
    public Guid BetalerKundeId { get; private set; }

    public DateOnly? Bryllupsdato { get; private set; }
    public int? Gæsteantal { get; private set; }
    public DateTimeOffset Oprettet { get; private set; }

    /// <summary>True when someone other than the couple is footing the bill.</summary>
    public bool HarSærskiltBetaler => BetalerKundeId != KundeId;

    public static Booking Create(Guid kundeId, DateOnly? bryllupsdato, int? gæsteantal)
    {
        if (kundeId == Guid.Empty)
        {
            throw new ArgumentException("En booking skal høre til en kunde.", nameof(kundeId));
        }

        if (!ErGyldigtGæsteantal(gæsteantal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gæsteantal), gæsteantal, UgyldigtGæsteantal);
        }

        return new Booking
        {
            Id = Guid.CreateVersion7(),
            KundeId = kundeId,
            BetalerKundeId = kundeId,
            Bryllupsdato = bryllupsdato,
            Gæsteantal = gæsteantal,
            Oprettet = DateTimeOffset.UtcNow
        };
    }

    public void SætGæsteantal(int? gæsteantal)
    {
        if (!ErGyldigtGæsteantal(gæsteantal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gæsteantal), gæsteantal, UgyldigtGæsteantal);
        }

        Gæsteantal = gæsteantal;
    }

    public void SætBryllupsdato(DateOnly? bryllupsdato) => Bryllupsdato = bryllupsdato;

    public void SætBetaler(Guid betalerKundeId)
    {
        if (betalerKundeId == Guid.Empty)
        {
            throw new ArgumentException("Betaleren skal være en kunde.", nameof(betalerKundeId));
        }

        BetalerKundeId = betalerKundeId;
    }

    /// <summary>Hands the bill back to the couple.</summary>
    public void NulstilBetaler() => BetalerKundeId = KundeId;

    public const string UgyldigtGæsteantal = "Gæsteantal kan ikke være negativt.";

    /// <summary>
    /// A guest count may be unknown, but it may not be negative. Exposed so the
    /// API can answer with a validation problem instead of restating the rule.
    /// </summary>
    public static bool ErGyldigtGæsteantal(int? gæsteantal) => gæsteantal is null or >= 0;
}
