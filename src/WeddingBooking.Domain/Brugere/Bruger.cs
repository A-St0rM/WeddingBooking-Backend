namespace WeddingBooking.Domain.Brugere;

/// <summary>
/// A member of staff, and the role they hold.
/// </summary>
/// <remarks>
/// Supabase owns the login; this owns who that login *is* to us. A Bruger is
/// created by us, keyed on email, and is bound to a Supabase account the first
/// time that person signs in. Someone who can authenticate but has no Bruger
/// row is not a user of this system and is refused — access is granted here,
/// deliberately, not by whoever can create a Supabase account.
/// </remarks>
public sealed class Bruger
{
    private Bruger() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Navn { get; private set; } = string.Empty;
    public Rolle Rolle { get; private set; }

    /// The Supabase account this person signs in with. Null until they first do.
    public Guid? AuthBrugerId { get; private set; }

    public DateTimeOffset Oprettet { get; private set; }
    public DateTimeOffset? SidsteLogin { get; private set; }

    public static Bruger Create(string email, string navn, Rolle rolle)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("En bruger skal have en e-mail.", nameof(email));
        }

        return new Bruger
        {
            Id = Guid.CreateVersion7(),
            Email = Normalise(email),
            Navn = navn.Trim(),
            Rolle = rolle,
            Oprettet = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Ties this person to the Supabase account they just signed in with.</summary>
    public void RegistrerLogin(Guid authBrugerId)
    {
        AuthBrugerId = authBrugerId;
        SidsteLogin = DateTimeOffset.UtcNow;
    }

    public void SkiftRolle(Rolle rolle) => Rolle = rolle;

    public static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
