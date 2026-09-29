namespace WeddingBooking.Domain.Common;

internal static class Text
{
    /// <summary>Trimmed, or null when there is nothing there.</summary>
    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException(message) : value.Trim();
}
