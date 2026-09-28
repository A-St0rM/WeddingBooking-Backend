namespace WeddingBooking.Api.Common;

internal static class Search
{
    /// <summary>
    /// The patterns a search term needs matched: one, or two when it names a couple ("tobias og cec"),
    /// so each name can be found on its own person.
    /// </summary>
    public static IReadOnlyList<string> CouplePatterns(string term) =>
        Domain.Customers.Customer.SplitCouple(term) is { } couple ? [Contains(couple.First), Contains(couple.Second)] : [Contains(term)];

    /// <summary>An ILIKE pattern matching the term anywhere, with the wildcards in the term itself escaped.</summary>
    public static string Contains(string term) =>
        "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
