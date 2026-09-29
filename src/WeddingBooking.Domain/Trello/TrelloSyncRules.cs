using System.Globalization;
using System.Text.RegularExpressions;

namespace WeddingBooking.Domain.Trello;

public enum CheckItemAction
{
    None,

    /// <summary>The item is not on the card yet: add it, in the system's state.</summary>
    AddToTrello,

    /// <summary>Only the system changed since the last sync: write its state to Trello.</summary>
    PushToTrello,

    /// <summary>Trello changed since the last sync: take its state.</summary>
    TakeFromTrello,
}

/// <summary>
/// The rules that keep a booking and its Trello card agreeing. Pure: states in, what to do out.
/// </summary>
public static partial class TrelloSyncRules
{
    /// <summary>The checklist on a booking's card that mirrors its readiness items, one item each.</summary>
    public const string ReadinessChecklist = "Klargøring (bookingsystem)";

    private const string BlockStart = "[bookingsystem]";
    private const string BlockEnd = "[/bookingsystem]";
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    /// <summary>
    /// Three-way: the system's state, Trello's, and the state both agreed on at the last sync. Whoever
    /// moved away from the agreed state changed it. Had both changed since — which the webhooks make
    /// rare — Trello's change is taken, since there is no telling which came last.
    /// </summary>
    public static CheckItemAction Decide(bool systemDone, bool? trelloDone, bool? syncedDone)
    {
        if (trelloDone is not { } trello) return CheckItemAction.AddToTrello;
        if (syncedDone is not { } synced) return trello == systemDone ? CheckItemAction.None : CheckItemAction.PushToTrello;
        if (trello != synced) return trello == systemDone ? CheckItemAction.None : CheckItemAction.TakeFromTrello;
        return systemDone != synced ? CheckItemAction.PushToTrello : CheckItemAction.None;
    }

    /// <summary>
    /// The card's name and description with the booking's own data written in. The system owns the
    /// date and the guest count: they are kept in a marked block in the description, and a "80 pax"
    /// in the name follows the guest count. Everything else on the card is left as written.
    /// </summary>
    public static (string Name, string Description) WithBooking(string name, string description, DateOnly date, int guests)
    {
        var block = $"{BlockStart}\nDato: {date.ToString("dddd 'den' d. MMMM yyyy", Danish)}\nGæster: {guests}\n{BlockEnd}";
        var start = description.IndexOf(BlockStart, StringComparison.Ordinal);
        var end = description.IndexOf(BlockEnd, StringComparison.Ordinal);
        var newDescription = start >= 0 && end > start
            ? description[..start] + block + description[(end + BlockEnd.Length)..]
            : string.IsNullOrWhiteSpace(description) ? block : $"{block}\n\n{description}";

        return (Pax().Replace(name, $"{guests} pax", 1), newDescription);
    }

    [GeneratedRegex(@"\b\d{1,3}\s*pax\b", RegexOptions.IgnoreCase)]
    private static partial Regex Pax();
}
