using Microsoft.AspNetCore.Mvc;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Features.Me;

[ApiController]
[Route("api/me")]
public sealed class MeController(CurrentUser currentUser) : ControllerBase
{
    /// <summary>Who the caller is, as this system understands them.</summary>
    [HttpGet]
    public ActionResult<MeResponse> Get()
    {
        var user = currentUser.User;
        return new MeResponse(user.Id, user.Name, user.Email, user.Role, user.Phone);
    }
}

/// <remarks>Role is the enum, not a string, so the generated client knows the five values.</remarks>
public sealed record MeResponse(Guid Id, string Name, string Email, Role Role, string? Phone);
