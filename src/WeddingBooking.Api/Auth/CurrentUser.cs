using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Auth;

/// <summary>
/// The member of staff making this request, resolved once by
/// <see cref="KnownUserMiddleware"/>. Controllers read it; nothing else sets it.
/// </summary>
public sealed class CurrentUser
{
    private User? user;

    public User User => user ?? throw new InvalidOperationException(
        "No current user. The endpoint is either anonymous or ran before KnownUserMiddleware.");

    public bool IsResolved => user is not null;

    internal void Set(User resolved) => user = resolved;
}
