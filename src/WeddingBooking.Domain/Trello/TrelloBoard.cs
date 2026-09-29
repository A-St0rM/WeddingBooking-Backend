namespace WeddingBooking.Domain.Trello;

/// <summary>A Trello board as the import needs it — the same whether it came from the API or a JSON export.</summary>
public sealed record TrelloBoard(string Id, string Name, IReadOnlyList<TrelloCard> Cards, IReadOnlyList<TrelloList>? Lists = null, string? Url = null);

public sealed record TrelloList(string Id, string Name, double Position, bool Closed);

public sealed record TrelloCard(
    string Id,
    string Name,
    string Description,
    DateTimeOffset? Due,
    string ListName,
    bool Closed,
    IReadOnlyList<TrelloLabel> Labels,
    IReadOnlyList<TrelloChecklist> Checklists,
    IReadOnlyDictionary<string, string> CustomFields,
    string ListId = "",
    double Position = 0,
    string? Url = null,
    bool DueComplete = false);

public sealed record TrelloLabel(string Name, string Color);

public sealed record TrelloChecklist(string Name, IReadOnlyList<TrelloCheckItem> Items, string Id = "");

public sealed record TrelloCheckItem(string Name, bool Complete, string Id = "");

/// <summary>Where boards come from: the board's open JSON address, or Trello's API when a key is set.</summary>
public interface ITrelloGateway
{
    /// <summary>A key and token are set. Reading a public board needs neither; writing to Trello needs both.</summary>
    bool HasKey { get; }

    Task<TrelloBoard> GetBoardAsync(string boardId, CancellationToken ct);

    // Writing needs the key and token; without them these refuse.

    Task<string> CreateCardAsync(string listId, string name, string description, CancellationToken ct);

    /// <summary>Changes what is given; null leaves it as it is.</summary>
    Task UpdateCardAsync(string cardId, string? name, string? description, string? listId, CancellationToken ct);

    Task<string> CreateChecklistAsync(string cardId, string name, CancellationToken ct);

    Task<string> AddCheckItemAsync(string checklistId, string name, bool complete, CancellationToken ct);

    Task SetCheckItemAsync(string cardId, string checkItemId, bool complete, CancellationToken ct);

    Task RegisterWebhookAsync(string boardId, string callbackUrl, CancellationToken ct);
}
