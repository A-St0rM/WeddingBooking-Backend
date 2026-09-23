using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Brugere;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Auth;

/// <summary>
/// Who is calling. Resolved once per request from the Supabase token.
/// </summary>
public interface IBrugerAccessor
{
    /// <summary>The caller, or null if they authenticated but are unknown to us.</summary>
    Task<Bruger?> HentAsync(CancellationToken cancellationToken);
}

public sealed class BrugerAccessor(
    IHttpContextAccessor httpContextAccessor,
    WeddingBookingDbContext database) : IBrugerAccessor
{
    private Bruger? resolved;
    private bool hasResolved;

    public async Task<Bruger?> HentAsync(CancellationToken cancellationToken)
    {
        if (hasResolved)
        {
            return resolved;
        }

        hasResolved = true;

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return resolved = null;
        }

        var authBrugerId = LæsAuthBrugerId(principal);
        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email");

        // Normally we know the Supabase account already.
        if (authBrugerId is not null)
        {
            resolved = await database.Brugere
                .FirstOrDefaultAsync(bruger => bruger.AuthBrugerId == authBrugerId, cancellationToken);
        }

        // First sign-in: the Bruger was created by us, keyed on email, and has
        // never been tied to a Supabase account. Tie it now.
        if (resolved is null && !string.IsNullOrWhiteSpace(email))
        {
            var normaliseret = Bruger.Normalise(email);
            resolved = await database.Brugere
                .FirstOrDefaultAsync(bruger => bruger.Email == normaliseret, cancellationToken);

            if (resolved is not null && authBrugerId is not null)
            {
                resolved.RegistrerLogin(authBrugerId.Value);
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        return resolved;
    }

    private static Guid? LæsAuthBrugerId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
