using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Auth;

/// <summary>
/// Limits an endpoint to the roles named. Everyone known to the system passes
/// KnownUserMiddleware; this is the second gate, for what their role may do.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowRolesAttribute(params Role[] roles) : Attribute, IAuthorizationFilter
{
    public IReadOnlyList<Role> Roles { get; } = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<CurrentUser>();
        if (currentUser.IsResolved && Roles.Contains(currentUser.User.Role)) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Ingen adgang",
            Detail = "Din rolle giver ikke adgang til denne handling.",
        })
        { StatusCode = StatusCodes.Status403Forbidden, ContentTypes = { "application/problem+json" } };
    }
}

/// <summary>The roles that run the sales side: the event manager and the owners.</summary>
public sealed class ManagementOnlyAttribute() : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new AllowRolesAttribute(Role.EventManager, Role.Owner);
}

/// <summary>The roles that may see prices and payments.</summary>
public sealed class PriceRolesOnlyAttribute() : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new AllowRolesAttribute(Role.EventManager, Role.Owner, Role.Bookkeeper);
}
