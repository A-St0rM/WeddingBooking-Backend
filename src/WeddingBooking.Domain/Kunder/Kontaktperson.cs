namespace WeddingBooking.Domain.Kunder;

/// <summary>
/// One of the two individuals behind a Kunde, each with their own email and
/// phone — the event manager writes to whichever of them actually answers.
/// </summary>
public sealed class Kontaktperson
{
    private Kontaktperson() { }

    public Guid Id { get; private set; }
    public Guid KundeId { get; private set; }
    public string Navn { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Telefon { get; private set; }

    /// <summary>The default recipient for anything sent to this Kunde.</summary>
    public bool ErPrimær { get; private set; }

    internal static Kontaktperson Create(Guid kundeId, string navn, string? email, string? telefon)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("En kontaktperson skal have et navn.", nameof(navn));
        }

        return new Kontaktperson
        {
            Id = Guid.CreateVersion7(),
            KundeId = kundeId,
            Navn = navn.Trim(),
            Email = Tom(email),
            Telefon = Tom(telefon)
        };
    }

    public void Ret(string navn, string? email, string? telefon)
    {
        if (string.IsNullOrWhiteSpace(navn))
        {
            throw new ArgumentException("En kontaktperson skal have et navn.", nameof(navn));
        }

        Navn = navn.Trim();
        Email = Tom(email);
        Telefon = Tom(telefon);
    }

    internal void SætPrimær(bool primær) => ErPrimær = primær;

    private static string? Tom(string? værdi) =>
        string.IsNullOrWhiteSpace(værdi) ? null : værdi.Trim();
}
