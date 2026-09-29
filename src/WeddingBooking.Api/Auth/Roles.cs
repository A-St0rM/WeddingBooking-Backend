using WeddingBooking.Domain.Users;

namespace WeddingBooking.Api.Auth;

/// <summary>Who may do what. One place, so a role's reach can be read in one go.</summary>
public static class Roles
{
    /// <summary>Kitchen and event production see the wedding, not what it costs.</summary>
    public static bool SeesPrices(this Role role) => role is Role.EventManager or Role.Owner or Role.Bookkeeper;
}
