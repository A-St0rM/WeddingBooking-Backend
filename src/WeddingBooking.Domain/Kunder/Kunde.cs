namespace WeddingBooking.Domain.Kunder;

/// <summary>
/// The Brudepar treated as one commercial entity — one name, one address, one
/// set of payment terms. See CONTEXT.md.
/// </summary>
/// <remarks>
/// This shape is not an accident: e-conomic can hold exactly one customer with
/// one name and one address, so that is what a Kunde is. The two people behind
/// it live as Kontaktpersoner.
///
/// <para>A Kunde outlives its Bookings. A couple returning for an anniversary
/// is the same customer, not a stranger — which is what makes history per
/// customer possible at all.</para>
/// </remarks>
public sealed class Kunde
{
    private readonly List<Kontaktperson> kontaktpersoner = [];

    private Kunde() { }

    public Guid Id { get; private set; }
    public string Navn { get; private set; } = string.Empty;
    public string? Adresse { get; private set; }
    public string? Postnr { get; private set; }
    public string? By { get; private set; }

    /// <summary>Null until this customer has been created in e-conomic.</summary>
    public int? EconomicCustomerNumber { get; private set; }

    public DateTimeOffset Oprettet { get; private set; }

    public IReadOnlyList<Kontaktperson> Kontaktpersoner => kontaktpersoner;

    /// <summary>Who to write to. The primary contact, or the first one there is.</summary>
    public Kontaktperson? PrimærKontakt =>
        kontaktpersoner.FirstOrDefault(k => k.ErPrimær) ?? kontaktpersoner.FirstOrDefault();

    public static Kunde Create(string navn, string? adresse = null, string? postnr = null, string? by = null)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("En kunde skal have et navn.", nameof(navn));
        }

        return new Kunde
        {
            Id = Guid.CreateVersion7(),
            Navn = navn.Trim(),
            Adresse = Tom(adresse),
            Postnr = Tom(postnr),
            By = Tom(by),
            Oprettet = DateTimeOffset.UtcNow
        };
    }

    public void Ret(string navn, string? adresse, string? postnr, string? by)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("En kunde skal have et navn.", nameof(navn));
        }

        Navn = navn.Trim();
        Adresse = Tom(adresse);
        Postnr = Tom(postnr);
        By = Tom(by);
    }

    /// <summary>The first contact added becomes primary; there is nobody else to be.</summary>
    public Kontaktperson TilføjKontaktperson(string navn, string? email, string? telefon)
    {
        var kontakt = Kontaktperson.Create(Id, navn, email, telefon);

        if (kontaktpersoner.Count == 0)
        {
            kontakt.SætPrimær(true);
        }

        kontaktpersoner.Add(kontakt);
        return kontakt;
    }

    /// <summary>Exactly one contact is primary. Promoting one demotes the other.</summary>
    public void GørPrimær(Guid kontaktpersonId)
    {
        var kontakt = kontaktpersoner.FirstOrDefault(k => k.Id == kontaktpersonId)
            ?? throw new InvalidOperationException("Kontaktpersonen hører ikke til denne kunde.");

        foreach (var anden in kontaktpersoner)
        {
            anden.SætPrimær(anden.Id == kontakt.Id);
        }
    }

    public void FjernKontaktperson(Guid kontaktpersonId)
    {
        var kontakt = kontaktpersoner.FirstOrDefault(k => k.Id == kontaktpersonId);
        if (kontakt is null)
        {
            return;
        }

        kontaktpersoner.Remove(kontakt);

        // Removing the primary must not leave the Kunde with nobody to write to.
        if (kontakt.ErPrimær && kontaktpersoner.Count > 0)
        {
            kontaktpersoner[0].SætPrimær(true);
        }
    }

    public void KobletTilEconomic(int customerNumber) => EconomicCustomerNumber = customerNumber;

    private static string? Tom(string? værdi) =>
        string.IsNullOrWhiteSpace(værdi) ? null : værdi.Trim();
}
