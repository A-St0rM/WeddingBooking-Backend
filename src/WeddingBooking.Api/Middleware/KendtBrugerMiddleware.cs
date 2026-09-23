using WeddingBooking.Api.Auth;

namespace WeddingBooking.Api.Middleware;

/// <summary>
/// Authenticating proves who you are. It does not make you a user of this system.
/// </summary>
/// <remarks>
/// Anyone with a Supabase account for this project can obtain a valid token.
/// Being *known here* is a separate thing, granted by an administrator creating
/// a Bruger row. This is the one place that distinction is enforced, so no
/// endpoint has to remember it.
/// </remarks>
public sealed class KendtBrugerMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IBrugerAccessor brugere)
    {
        var isApiCall = context.Request.Path.StartsWithSegments("/api");
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (isApiCall && isAuthenticated && await brugere.HentAsync(context.RequestAborted) is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Ukendt bruger",
                detail = "Du er logget ind, men har ikke adgang til bookingsystemet. Kontakt event manageren."
            });
            return;
        }

        await next(context);
    }
}
