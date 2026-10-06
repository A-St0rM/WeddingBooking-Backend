using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Trello;

namespace WeddingBooking.Integrations.Trello;

public sealed class TrelloOptions
{
    public string? ApiKey { get; set; }
    public string? Token { get; set; }

    /// <summary>The year boards shown under Trello — Bryllup 26, 27, 28.</summary>
    public string[] BoardIds { get; set; } = [];

    /// <summary>The Trello app's secret: what Trello signs its webhooks with.</summary>
    public string? AppSecret { get; set; }

    /// <summary>Where Trello sends its webhooks — this API's public address + /api/trello/webhook.</summary>
    public string? CallbackUrl { get; set; }
}

/// <summary>
/// Reads a board. A public board is read from its open JSON address with no key (§11.16);
/// with a key and token set, through Trello's REST API instead, which also reaches private boards.
/// Both give the same JSON, read by <see cref="TrelloJson"/>.
/// </summary>
internal sealed class TrelloApiGateway(HttpClient http, IOptions<TrelloOptions> options) : ITrelloGateway
{
    public bool HasKey => !string.IsNullOrWhiteSpace(options.Value.ApiKey) && !string.IsNullOrWhiteSpace(options.Value.Token);

    public async Task<TrelloBoard> GetBoardAsync(string boardId, CancellationToken ct)
    {
        // The REST address reads a public board without a key, by its long id. The open
        // trello.com/b/{id}.json only knows the short link, and answers the long id with a web page.
        var id = Uri.EscapeDataString(boardId);
        var url = $"https://api.trello.com/1/boards/{id}?" + string.Join("&",
            "lists=all", "cards=all", "card_fields=all", "checklists=all", "customFields=true", "card_customFieldItems=true");
        if (HasKey) url += $"&key={Uri.EscapeDataString(options.Value.ApiKey!)}&token={Uri.EscapeDataString(options.Value.Token!)}";
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException(HasKey
                ? $"Trello svarede {(int)response.StatusCode} for bordet {boardId}."
                : $"Trello svarede {(int)response.StatusCode} for bordet {boardId}. Uden nøgle kan kun offentlige boards læses.");
        }

        return TrelloJson.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<string> CreateCardAsync(string listId, string name, string description, CancellationToken ct) =>
        await IdOfAsync(HttpMethod.Post, "cards", new() { ["idList"] = listId, ["name"] = name, ["desc"] = description }, ct);

    public async Task UpdateCardAsync(string cardId, string? name, string? description, string? listId, CancellationToken ct)
    {
        var fields = new Dictionary<string, string?> { ["name"] = name, ["desc"] = description, ["idList"] = listId }
            .Where(f => f.Value is not null).ToDictionary(f => f.Key, f => f.Value!);
        if (fields.Count > 0) await WriteAsync(HttpMethod.Put, $"cards/{Uri.EscapeDataString(cardId)}", fields, ct);
    }

    public Task<string> CreateChecklistAsync(string cardId, string name, CancellationToken ct) =>
        IdOfAsync(HttpMethod.Post, $"cards/{Uri.EscapeDataString(cardId)}/checklists", new() { ["name"] = name }, ct);

    public Task<string> AddCheckItemAsync(string checklistId, string name, bool complete, CancellationToken ct) =>
        IdOfAsync(HttpMethod.Post, $"checklists/{Uri.EscapeDataString(checklistId)}/checkItems",
            new() { ["name"] = name, ["checked"] = complete ? "true" : "false" }, ct);

    public async Task SetCheckItemAsync(string cardId, string checkItemId, bool complete, CancellationToken ct) =>
        await WriteAsync(HttpMethod.Put, $"cards/{Uri.EscapeDataString(cardId)}/checkItem/{Uri.EscapeDataString(checkItemId)}",
            new() { ["state"] = complete ? "complete" : "incomplete" }, ct);

    public async Task RegisterWebhookAsync(string boardId, string callbackUrl, CancellationToken ct) =>
        await WriteAsync(HttpMethod.Post, "webhooks", new() { ["idModel"] = boardId, ["callbackURL"] = callbackUrl, ["description"] = "Bryllupsbooking" }, ct);

    private async Task<string> IdOfAsync(HttpMethod method, string path, Dictionary<string, string> fields, CancellationToken ct)
    {
        var body = await WriteAsync(method, path, fields, ct);
        return System.Text.Json.Nodes.JsonNode.Parse(body)?["id"]?.ToString()
            ?? throw new DomainException("Trello svarede uden et id.");
    }

    /// <summary>Trello takes its parameters in the query string; the key and token go the same way.</summary>
    private async Task<string> WriteAsync(HttpMethod method, string path, Dictionary<string, string> fields, CancellationToken ct)
    {
        if (!HasKey) throw new DomainException("Der kræves en Trello-nøgle og et token for at skrive til Trello.");
        fields["key"] = options.Value.ApiKey!;
        fields["token"] = options.Value.Token!;
        var query = string.Join("&", fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"));
        using var response = await http.SendAsync(new HttpRequestMessage(method, $"https://api.trello.com/1/{path}?{query}"), ct);
        if (!response.IsSuccessStatusCode) throw new DomainException($"Trello afviste ændringen ({(int)response.StatusCode}).");
        return await response.Content.ReadAsStringAsync(ct);
    }
}
