using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Users;

/// <summary>
/// A member of staff and the role they hold.
/// </summary>
/// <remarks>
/// Supabase owns the login; this owns who that login is to us. A user is created
/// here, keyed on email, and bound to a Supabase account the first time that
/// person signs in. Being able to authenticate is not the same as having access:
/// anyone with a Supabase account on the project can get a valid token, and
/// only a row here lets them in.
/// </remarks>
public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    /// <summary>Used where a message names someone to call — Bryan's number in a viewing confirmation.</summary>
    public string? Phone { get; private set; }

    public Role Role { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>The Supabase account this person signs in with; null until they first do.</summary>
    public Guid? AuthUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public static User Create(string email, string name, Role role, string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainException("En bruger skal have en gyldig e-mail.");
        }

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormaliseEmail(email),
            Name = string.IsNullOrWhiteSpace(name) ? NormaliseEmail(email) : name.Trim(),
            Phone = Blank(phone),
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, Role role, string? phone, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("En bruger skal have et navn.");
        }

        Name = name.Trim();
        Role = role;
        Phone = Blank(phone);
        IsActive = isActive;
    }

    /// <summary>Ties this person to the Supabase account they just signed in with.</summary>
    public void RecordLogin(Guid authUserId)
    {
        AuthUserId = authUserId;
        LastLoginAt = DateTimeOffset.UtcNow;
    }

    public static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
