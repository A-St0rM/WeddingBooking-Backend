namespace WeddingBooking.Api.Configuration;

public sealed class TrelloOptions
{
    public const string SectionName = "Trello";

    public string BaseUrl { get; init; } = "https://api.trello.com/1";
    public string ApiKey { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;

    /// One board per year: "2026" -> board id. Board-scoped ids (lists,
    /// labels, custom fields) are resolved by name at runtime, never hardcoded.
    public Dictionary<string, string> BoardIdByYear { get; init; } = [];
}
