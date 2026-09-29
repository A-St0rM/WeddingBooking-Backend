using System.Text.Json.Nodes;
using WeddingBooking.Domain.Trello;

namespace WeddingBooking.Integrations.Trello;

/// <summary>
/// Reads a board in Trello's own JSON — what "Export as JSON" gives, and what the
/// API's board endpoint gives with cards, lists, checklists and custom fields.
/// </summary>
public static class TrelloJson
{
    public static TrelloBoard Parse(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new FormatException("Filen er ikke gyldig JSON. Brug Trellos \"Eksportér som JSON\".");
        }

        if (root?["cards"] is not JsonArray cards) throw new FormatException("Filen ligner ikke en Trello-eksport: der er ingen kort.");

        var boardLists = (root["lists"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(l => new TrelloList(Str(l, "id"), Str(l, "name"), Num(l, "pos"), Bool(l, "closed")))
            .ToList();
        var lists = boardLists.ToDictionary(l => l.Id, l => l.Name);
        var fieldNames = (root["customFields"] as JsonArray ?? []).OfType<JsonObject>()
            .ToDictionary(f => Str(f, "id"), f => Str(f, "name"));
        var checklistsByCard = (root["checklists"] as JsonArray ?? []).OfType<JsonObject>()
            .GroupBy(c => Str(c, "idCard"))
            .ToDictionary(g => g.Key, g => g.Select(Checklist).ToList());

        var parsed = cards.OfType<JsonObject>().Select(card =>
        {
            var id = Str(card, "id");
            var checklists = card["checklists"] is JsonArray embedded
                ? embedded.OfType<JsonObject>().Select(Checklist).ToList()
                : checklistsByCard.GetValueOrDefault(id) ?? [];

            var fields = new Dictionary<string, string>();
            foreach (var item in (card["customFieldItems"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (!fieldNames.TryGetValue(Str(item, "idCustomField"), out var name)) continue;
                var value = item["value"] as JsonObject;
                var text = value?["number"]?.ToString() ?? value?["text"]?.ToString() ?? value?["date"]?.ToString();
                if (text is not null) fields[name] = text;
            }

            return new TrelloCard(
                id,
                Str(card, "name"),
                Str(card, "desc"),
                DateTimeOffset.TryParse(card["due"]?.ToString(), out var due) ? due : null,
                lists.GetValueOrDefault(Str(card, "idList")) ?? "",
                card["closed"]?.GetValue<bool>() ?? false,
                (card["labels"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new TrelloLabel(Str(l, "name"), Str(l, "color"))).ToList(),
                checklists,
                fields,
                Str(card, "idList"),
                Num(card, "pos"),
                card["shortUrl"]?.ToString() ?? card["url"]?.ToString(),
                Bool(card, "dueComplete"));
        }).ToList();

        return new TrelloBoard(Str(root, "id"), Str(root, "name"), parsed, boardLists, root["shortUrl"]?.ToString() ?? root["url"]?.ToString());
    }

    private static TrelloChecklist Checklist(JsonObject checklist) => new(
        Str(checklist, "name"),
        (checklist["checkItems"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(i => new TrelloCheckItem(Str(i, "name"), Str(i, "state") == "complete")).ToList());

    private static string Str(JsonNode node, string key) => node[key]?.ToString() ?? "";

    private static double Num(JsonNode node, string key) =>
        double.TryParse(node[key]?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static bool Bool(JsonNode node, string key) => node[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
