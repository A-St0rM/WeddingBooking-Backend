using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

internal static class Json
{
    public static async Task<JsonNode> ReadAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {body}");
        return JsonNode.Parse(body)!;
    }

    public static async Task<JsonNode> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        await (await client.PostAsJsonAsync(url, body ?? new { })).ReadAsync();

    public static async Task<JsonNode> PutJsonAsync(this HttpClient client, string url, object body) =>
        await (await client.PutAsJsonAsync(url, body)).ReadAsync();

    public static async Task<JsonNode> GetJsonAsync(this HttpClient client, string url) =>
        await (await client.GetAsync(url)).ReadAsync();

    /// <summary>The two people for a new booking: "Tobias og Cecilie" → Tobias and Cecilie; one word gets a second person.</summary>
    public static object[] Couple(string name, string? firstEmail = null)
    {
        var parts = name.Split(" og ", 2);
        return
        [
            new { firstName = parts[0], lastName = (string?)null, email = firstEmail, phone = (string?)null },
            new { firstName = parts.Length > 1 ? parts[1] : "Partner", lastName = (string?)null, email = (string?)null, phone = (string?)null },
        ];
    }

    public static decimal Dec(this JsonNode? node) => node!.GetValue<decimal>();
    public static string Str(this JsonNode? node) => node!.GetValue<string>();

    /// <summary>Every property name anywhere in the document.</summary>
    public static IEnumerable<string> PropertyNames(JsonNode? node) => node switch
    {
        JsonObject o => o.SelectMany(p => PropertyNames(p.Value).Prepend(p.Key)),
        JsonArray a => a.SelectMany(PropertyNames),
        _ => [],
    };

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
