namespace WeddingBooking.Api.Common;

internal static class Search
{
    /// <summary>An ILIKE pattern matching the term anywhere, with the wildcards in the term itself escaped.</summary>
    public static string Contains(string term) =>
        "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
