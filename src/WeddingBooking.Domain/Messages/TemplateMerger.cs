using System.Text.RegularExpressions;

namespace WeddingBooking.Domain.Messages;

public sealed record MergeResult(string Text, IReadOnlyList<string> Missing);

/// <summary>
/// Fills {{placeholders}} from the booking. A placeholder the booking has no value
/// for is left visible in the text and reported, so it is seen before sending
/// rather than sent as a blank.
/// </summary>
public static partial class TemplateMerger
{
    /// <summary>The placeholders a template can use, with what each one is.</summary>
    public static readonly IReadOnlyDictionary<string, string> Placeholders = new Dictionary<string, string>
    {
        ["parret"] = "Parrets navn, fx Tobias og Cecilie",
        ["fornavn"] = "Den primære kontakts fornavn",
        ["dato"] = "Bryllupsdatoen, fx lørdag den 12. juni 2027",
        ["gæster"] = "Gæsteantallet",
        ["pris"] = "Bryllupsets samlede pris",
        ["fremvisning"] = "Tidspunktet for fremvisningen",
        ["vært"] = "Navnet på den, der viser rundt",
        ["vært_telefon"] = "Telefonnummeret på den, der viser rundt",
        ["afsender"] = "Dit navn",
    };

    public static MergeResult Merge(string template, IReadOnlyDictionary<string, string?> values)
    {
        var missing = new List<string>();
        var text = PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value.Trim().ToLowerInvariant();
            if (values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
            if (!missing.Contains(name)) missing.Add(name);
            return match.Value;
        });
        return new MergeResult(text, missing);
    }

    [GeneratedRegex(@"\{\{\s*([\p{L}_]+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}
