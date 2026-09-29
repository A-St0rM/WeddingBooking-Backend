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
        var id = Uri.EscapeDataString(boardId);
        var url = HasKey
            ? $"https://api.trello.com/1/boards/{id}?" + string.Join("&",
                "lists=all", "cards=all", "card_fields=all", "checklists=all", "customFields=true", "card_customFieldItems=true",
                $"key={Uri.EscapeDataString(options.Value.ApiKey!)}", $"token={Uri.EscapeDataString(options.Value.Token!)}")
            : $"https://trello.com/b/{id}.json";
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException(HasKey
                ? $"Trello svarede {(int)response.StatusCode} for bordet {boardId}."
                : $"Trello svarede {(int)response.StatusCode} for bordet {boardId}. Uden nøgle kan kun offentlige boards læses.");
        }

        return TrelloJson.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
