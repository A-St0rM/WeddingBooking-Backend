namespace WeddingBooking.Domain.Bookinger;

/// <summary>
/// One wedding, from first enquiry to final invoice. See CONTEXT.md.
/// </summary>
/// <remarks>
/// A Booking is valid with partial information: a couple who ring with a date
/// but no firm guest count still get recorded. Nothing here is rejected for
/// being incomplete.
///
/// <para><b>Kundenavn</b> is the couple as one commercial name ("Anna Nielsen
/// &amp; Mikkel Sørensen") — a Kunde, per CONTEXT.md, not a Brudepar. It is a
/// bare string only until ticket 04 gives it a real Kunde with two
/// Kontaktpersoner; then it becomes Kunde.Navn.</para>
/// </remarks>
public sealed class Booking
{
    private Booking() { }

    public Guid Id { get; private set; }
    public string? Kundenavn { get; private set; }
    public DateOnly? Bryllupsdato { get; private set; }
    public int? Gæsteantal { get; private set; }
    public DateTimeOffset Oprettet { get; private set; }

    public static Booking Create(string? kundenavn, DateOnly? bryllupsdato, int? gæsteantal)
    {
        if (!ErGyldigtGæsteantal(gæsteantal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(gæsteantal), gæsteantal, UgyldigtGæsteantal);
        }

        return new Booking
        {
            Id = Guid.CreateVersion7(),
            Kundenavn = Normalise(kundenavn),
            Bryllupsdato = bryllupsdato,
            Gæsteantal = gæsteantal,
            Oprettet = DateTimeOffset.UtcNow
        };
    }

    public const string UgyldigtGæsteantal = "Gæsteantal kan ikke være negativt.";

    /// <summary>
    /// A guest count may be unknown, but it may not be negative. Exposed so the
    /// API can answer with a validation problem instead of restating the rule.
    /// </summary>
    public static bool ErGyldigtGæsteantal(int? gæsteantal) => gæsteantal is null or >= 0;

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
