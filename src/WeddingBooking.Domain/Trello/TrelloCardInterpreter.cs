using System.Globalization;
using System.Text.RegularExpressions;
using WeddingBooking.Domain.Readiness;

namespace WeddingBooking.Domain.Trello;

/// <summary>What a card turned out to say — or why it could not be read.</summary>
public sealed record CardReading(
    string CardId,
    string CardName,
    string? CoupleName,
    DateOnly? EventDate,
    int? GuestCount,
    IReadOnlyList<string> Emails,
    IReadOnlyList<string> Phones,
    IReadOnlyDictionary<ReadinessCategory, ReadinessStatus> Readiness,
    IReadOnlyList<string> Labels,
    string? SkipReason)
{
    public bool CanImport => SkipReason is null;
}

/// <summary>
/// Reads a wedding out of a Trello card. Cards are written by hand and never the
/// same way twice, so every rule here is lenient, and a card the rules cannot
/// read is reported with the reason instead of being guessed at.
/// </summary>
public static partial class TrelloCardInterpreter
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    private static readonly (ReadinessCategory Category, string[] Words)[] ChecklistWords =
    [
        (ReadinessCategory.Contact, ["kontakt"]),
        (ReadinessCategory.TableSetting, ["opdækning", "opstilling", "borde"]),
        (ReadinessCategory.Decoration, ["pynt", "blomster"]),
        (ReadinessCategory.Food, ["mad", "menu", "kuvert", "allergi"]),
        (ReadinessCategory.PostEventInvoicing, ["efterfaktur", "tillæg"]),
    ];

    public static CardReading Read(TrelloCard card, int? boardYear)
    {
        var text = $"{card.Name}\n{card.Description}";
        var guests = Field(card, "gæst", "pax", "kuvert") is { } g && int.TryParse(Digits().Match(g).Value, out var fromField)
            ? fromField
            : GuestCount(text);
        var date = Field(card, "dato") is { } d && ParseDate(d, boardYear) is { } fromDateField
            ? fromDateField
            : card.Due is { } due ? DateOnly.FromDateTime(due.UtcDateTime) : ParseDate(text, boardYear);
        var couple = CoupleName(card);

        string? skip = null;
        if (card.Closed) skip = "Kortet er arkiveret i Trello.";
        else if (IsTemplate(card)) skip = "Skabelonkort — ikke et bryllup.";
        else if (date is null) skip = "Ingen bryllupsdato: hverken forfaldsdato, datofelt eller en dato i navn eller beskrivelse.";
        else if (guests is null) skip = "Intet gæsteantal: skriv fx \"80 pax\" i kortets navn eller beskrivelse.";
        else if (guests is < 1 or > 350) skip = $"Gæsteantallet {guests} ligger uden for 1–350.";
        else if (Customers.Customer.SplitCouple(couple) is null) skip = "Parrets navn kan ikke deles i to personer: skriv fx \"Tobias & Cecilie\" i kortets navn.";

        return new CardReading(
            card.Id, card.Name, couple, date, guests,
            Emails().Matches(card.Description).Select(m => m.Value.ToLowerInvariant()).Distinct().ToList(),
            Phones().Matches(card.Description).Select(m => m.Value.Trim()).Distinct().ToList(),
            ReadinessOf(card),
            card.Labels.Select(l => string.IsNullOrWhiteSpace(l.Name) ? l.Color : l.Name).ToList(),
            skip);
    }

    /// <summary>"Bryllup 27" → 2027. Helps read "12. juni" without a year.</summary>
    public static int? YearOf(string boardName) =>
        TwoOrFourDigitYear().Match(boardName) is { Success: true } m
            ? m.Value.Length == 2 ? 2000 + int.Parse(m.Value, Danish) : int.Parse(m.Value, Danish)
            : null;

    private static string? Field(TrelloCard card, params string[] names) =>
        card.CustomFields.FirstOrDefault(f => names.Any(n => f.Key.Contains(n, StringComparison.OrdinalIgnoreCase))).Value;

    private static int? GuestCount(string text) =>
        GuestPattern().Match(text) is { Success: true } m ? int.Parse(m.Groups[1].Value, Danish) : null;

    private static DateOnly? ParseDate(string text, int? year)
    {
        if (NumericDate().Match(text) is { Success: true } numeric)
        {
            var y = numeric.Groups[3].Success ? int.Parse(numeric.Groups[3].Value, Danish) : year;
            if (y is < 100) y += 2000;
            if (y is not null && DateOnly.TryParseExact(
                    $"{int.Parse(numeric.Groups[1].Value, Danish):00}-{int.Parse(numeric.Groups[2].Value, Danish):00}-{y}", "dd-MM-yyyy", out var d))
            {
                return d;
            }
        }

        if (WrittenDate().Match(text) is { Success: true } written)
        {
            var y = written.Groups[3].Success ? int.Parse(written.Groups[3].Value, Danish) : year;
            var months = Danish.DateTimeFormat.MonthNames;
            var month = Array.FindIndex(months, name => name.Length > 0 && name.StartsWith(written.Groups[2].Value.ToLowerInvariant()[..3], StringComparison.Ordinal)) + 1;
            if (y is not null && month > 0 && int.Parse(written.Groups[1].Value, Danish) is var day && day <= DateTime.DaysInMonth(y.Value, month))
            {
                return new DateOnly(y.Value, month, day);
            }
        }

        return null;
    }

    /// <summary>The name without the guest count and the word "Bryllup": "Bryllup Tobias & Cecilie 80 pax" → "Tobias og Cecilie".</summary>
    private static string? CoupleName(TrelloCard card)
    {
        var name = GuestPattern().Replace(card.Name, "");
        name = NumericDate().Replace(name, "");
        name = WrittenDate().Replace(name, "");
        name = BryllupWord().Replace(name, "");
        name = LooseSeparators().Replace(name, " ");
        name = name.Replace("&", " og ").Replace("+", " og ");
        name = Whitespace().Replace(name, " ").Trim(' ', '-', '–', ',', ':');
        return name.Length >= 2 ? name : null;
    }

    private static bool IsTemplate(TrelloCard card) =>
        card.Name.Contains("skabelon", StringComparison.OrdinalIgnoreCase) || card.Name.Contains("template", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<ReadinessCategory, ReadinessStatus> ReadinessOf(TrelloCard card)
    {
        var result = new Dictionary<ReadinessCategory, ReadinessStatus>();
        foreach (var checklist in card.Checklists)
        {
            var match = ChecklistWords.FirstOrDefault(c => c.Words.Any(w => checklist.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
            if (match.Words is null || checklist.Items.Count == 0) continue;

            var done = checklist.Items.Count(i => i.Complete);
            result[match.Category] = done == checklist.Items.Count ? ReadinessStatus.Done : done > 0 ? ReadinessStatus.InProgress : ReadinessStatus.NotStarted;
        }
        return result;
    }

    [GeneratedRegex(@"(\d{1,3})\s*(?:pax|gæster|gæst|personer|pers\.?|kuverter)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GuestPattern();

    [GeneratedRegex(@"\b(\d{1,2})[./-](\d{1,2})(?:[./-](\d{2,4}))?\b")]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"\b(\d{1,2})\.?\s+(jan|feb|mar|apr|maj|jun|jul|aug|sep|okt|nov|dec)[a-zæøå]*\.?(?:\s+(\d{4}))?", RegexOptions.IgnoreCase)]
    private static partial Regex WrittenDate();

    [GeneratedRegex(@"\b(?:20)?(\d{2})\b")]
    private static partial Regex TwoOrFourDigitYear();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\bbryllup(?:et)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex BryllupWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"(?:^|\s)[-–,:·]+(?=\s|$)")]
    private static partial Regex LooseSeparators();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex Emails();

    [GeneratedRegex(@"(?:\+45\s?)?(?:\d{2}\s?){3}\d{2}\b")]
    private static partial Regex Phones();
}
