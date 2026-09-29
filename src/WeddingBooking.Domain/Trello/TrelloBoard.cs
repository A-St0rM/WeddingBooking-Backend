namespace WeddingBooking.Domain.Trello;

/// <summary>A Trello board as the import needs it — the same whether it came from the API or a JSON export.</summary>
public sealed record TrelloBoard(string Id, string Name, IReadOnlyList<TrelloCard> Cards);

public sealed record TrelloCard(
    string Id,
    string Name,
    string Description,
    DateTimeOffset? Due,
    string ListName,
    bool Closed,
    IReadOnlyList<TrelloLabel> Labels,
    IReadOnlyList<TrelloChecklist> Checklists,
    IReadOnlyDictionary<string, string> CustomFields);

public sealed record TrelloLabel(string Name, string Color);

public sealed record TrelloChecklist(string Name, IReadOnlyList<TrelloCheckItem> Items);

public sealed record TrelloCheckItem(string Name, bool Complete);

/// <summary>Where boards come from: Trello's API with a key, or an uploaded JSON export.</summary>
public interface ITrelloGateway
{
    bool IsConfigured { get; }

    Task<TrelloBoard> GetBoardAsync(string boardId, CancellationToken ct);
}
