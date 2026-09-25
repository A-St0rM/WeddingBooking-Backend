namespace WeddingBooking.Api.Configuration;

public sealed class AccessOptions
{
    public const string SectionName = "Access";

    /// <summary>
    /// Solves the first-login problem: someone has to exist before anyone can
    /// create users. A signed-in person whose email is listed here and who has no
    /// user row yet is created as an event manager. Everyone else is created by
    /// an event manager or owner.
    /// </summary>
    public string[] BootstrapAdminEmails { get; init; } = [];
}
