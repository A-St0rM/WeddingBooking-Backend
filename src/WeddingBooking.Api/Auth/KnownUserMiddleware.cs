using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeddingBooking.Api.Configuration;
using WeddingBooking.Domain.Users;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Auth;

/// <summary>
/// Authenticating proves who you are. It does not make you a user of this system.
/// </summary>
/// <remarks>
/// This is the one place that distinction is enforced, so no endpoint has to
/// remember it. A signed-in person is looked up by their Supabase id; failing
/// that, by email — the first time they sign in, which binds the two. Nobody
/// found, or a deactivated user, gets a 403 that says why.
/// </remarks>
public sealed class KnownUserMiddleware(RequestDelegate next, ILogger<KnownUserMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext database,
        CurrentUser currentUser,
        IOptions<AccessOptions> access)
    {
        var isApiCall = context.Request.Path.StartsWithSegments("/api");
        if (!isApiCall || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var user = await ResolveAsync(context, database, access.Value);

        if (user is null || !user.IsActive)
        {
            logger.LogInformation("Refused signed-in caller {Email}: {Reason}",
                context.User.FindFirst("email")?.Value, user is null ? "no user row" : "deactivated");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Ingen adgang",
                    Detail = "Du er logget ind, men har ikke adgang til bookingsystemet. Kontakt event manageren."
                },
                options: null,
                contentType: "application/problem+json");
            return;
        }

        currentUser.Set(user);
        await next(context);
    }

    private static async Task<User?> ResolveAsync(HttpContext context, AppDbContext database, AccessOptions access)
    {
        var authUserId = Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : (Guid?)null;
        var email = context.User.FindFirst("email")?.Value;

        if (authUserId is not null)
        {
            var known = await database.Users.FirstOrDefaultAsync(u => u.AuthUserId == authUserId, context.RequestAborted);
            if (known is not null)
            {
                return known;
            }
        }

        if (string.IsNullOrWhiteSpace(email) || authUserId is null)
        {
            return null;
        }

        var normalised = User.NormaliseEmail(email);
        var byEmail = await database.Users.FirstOrDefaultAsync(u => u.Email == normalised, context.RequestAborted);

        if (byEmail is null && access.BootstrapAdminEmails.Any(e => User.NormaliseEmail(e) == normalised))
        {
            byEmail = User.Create(normalised, context.User.FindFirst("name")?.Value ?? normalised, Role.EventManager);
            database.Users.Add(byEmail);
        }

        if (byEmail is null)
        {
            return null;
        }

        byEmail.RecordLogin(authUserId.Value);
        await database.SaveChangesAsync(context.RequestAborted);
        return byEmail;
    }
}
