using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Users;

namespace WeddingBooking.Domain.Readiness;

/// <summary>The parts of getting a wedding ready — the sub-items on today's Trello card.</summary>
public enum ReadinessCategory
{
    Contact,
    TableSetting,
    Decoration,
    Food,

    /// <summary>Extras to invoice after the party. Not part of being ready; it comes after.</summary>
    PostEventInvoicing,
}

/// <summary>Trello's grey, half-green and green.</summary>
public enum ReadinessStatus
{
    NotStarted,
    InProgress,
    Done,
}

public sealed class ReadinessItem
{
    private ReadinessItem() { }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public ReadinessCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ReadinessStatus Status { get; private set; }

    /// <summary>What there is to know — for food: menu choices, covers, allergies.</summary>
    public string? Notes { get; private set; }

    public string? Responsible { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    internal static ReadinessItem Create(Guid bookingId, ReadinessCategory category, string title) => new()
    {
        Id = Guid.CreateVersion7(),
        BookingId = bookingId,
        Category = category,
        Title = Text.Required(title, "Et klargøringspunkt skal have en titel."),
        Status = ReadinessStatus.NotStarted,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    internal void Update(ReadinessStatus status, string? notes, string? responsible, string by)
    {
        if (notes?.Length > 5_000) throw new DomainException("Noten er for lang.");
        Status = status;
        Notes = Text.Blank(notes);
        Responsible = Text.Blank(responsible);
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }
}

public static class ReadinessRules
{
    /// <summary>The items every booking starts with, one per part of the Trello card.</summary>
    public static readonly IReadOnlyList<(ReadinessCategory Category, string Title)> Defaults =
    [
        (ReadinessCategory.Contact, "Kontakt"),
        (ReadinessCategory.TableSetting, "Opdækning og opstilling"),
        (ReadinessCategory.Decoration, "Pynt og blomster"),
        (ReadinessCategory.Food, "Mad: menu, kuverter og allergier"),
        (ReadinessCategory.PostEventInvoicing, "Efterfakturering"),
    ];

    /// <summary>Ready when every item before the day is done. Nobody sets it; it follows.</summary>
    public static bool AllDone(IEnumerable<ReadinessItem> items)
    {
        var beforeTheDay = items.Where(item => item.Category != ReadinessCategory.PostEventInvoicing).ToList();
        return beforeTheDay.Count > 0 && beforeTheDay.All(item => item.Status == ReadinessStatus.Done);
    }

    /// <summary>Extras noted after the party and not yet invoiced: the item is in progress.</summary>
    public static bool PostEventOutstanding(IEnumerable<ReadinessItem> items) =>
        items.Any(item => item.Category == ReadinessCategory.PostEventInvoicing && item.Status == ReadinessStatus.InProgress);

    /// <summary>Who may change which part. Everyone may read it.</summary>
    public static bool CanEdit(Role role, ReadinessCategory category) => role switch
    {
        Role.EventManager or Role.Owner => true,
        Role.Chef => category == ReadinessCategory.Food,
        Role.EventProducer => category is ReadinessCategory.Contact or ReadinessCategory.TableSetting or ReadinessCategory.Decoration,
        Role.Bookkeeper => category == ReadinessCategory.PostEventInvoicing,
        _ => false,
    };
}
