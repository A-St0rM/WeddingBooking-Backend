using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Trello;

namespace WeddingBooking.Integrations.Trello;

public sealed class TrelloOptions
{
    public string? ApiKey { get; set; }
    public string? Token { get; set; }
}

/// <summary>
/// Reads a board through Trello's REST API. Without a key and token it is not
/// configured, and the import takes an uploaded JSON export instead.
/// </summary>
internal sealed class TrelloApiGateway(HttpClient http, IOptions<TrelloOptions> options) : ITrelloGateway
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey) && !string.IsNullOrWhiteSpace(options.Value.Token);

    public async Task<TrelloBoard> GetBoardAsync(string boardId, CancellationToken ct)
    {
        if (!IsConfigured) throw new DomainException("Trello-nøglen er ikke sat op. Upload bordets JSON-eksport i stedet.");

        var query = string.Join("&",
            "lists=all", "cards=all", "card_fields=all", "checklists=all", "customFields=true", "card_customFieldItems=true",
            $"key={Uri.EscapeDataString(options.Value.ApiKey!)}", $"token={Uri.EscapeDataString(options.Value.Token!)}");
        using var response = await http.GetAsync($"https://api.trello.com/1/boards/{Uri.EscapeDataString(boardId)}?{query}", ct);
        if (!response.IsSuccessStatusCode) throw new DomainException($"Trello svarede {(int)response.StatusCode} for bordet {boardId}.");

        return TrelloJson.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
