using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Summaries;

namespace WeddingBooking.Integrations.Summaries;

public sealed class OpenAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-5-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

/// <summary>
/// The summary from OpenAI, as four fixed fields through structured output. The
/// model is told to leave numbers out: a language model guessing a guest count
/// from a mail thread guesses wrong, so the booking's own data is shown instead.
/// </summary>
internal sealed class OpenAiSummariser(HttpClient http, IOptions<OpenAiOptions> options) : ISummariser
{
    public string Source => $"OpenAI ({options.Value.Model})";

    private const string Instructions = """
        Du hjælper event manageren på Engestofte Gods med at genoptage en bryllupssag.
        Skriv på dansk, kort og konkret, i fire felter:
        - status: hvor sagen står nu, i én eller to sætninger.
        - agreedContent: det, parret og godset har aftalt om indholdet (mad, drikke, vielse, morgenmad …).
        - openItems: de åbne punkter, hver som en kort linje, fx "Morgenmad — afventer svar".
        - lastContact: hvornår der sidst var kontakt, og hvad der blev sagt.
        Nævn ikke gæsteantal, datoer for brylluppet eller beløb; de vises separat fra bookingens egne data.
        Opfind intet. Står noget ikke i teksten, så skriv, at det ikke fremgår.
        Du foreslår kun. Du sender og ændrer intet.
        """;

    private static readonly JsonObject Schema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["status", "agreedContent", "openItems", "lastContact"],
          "properties": {
            "status": { "type": "string" },
            "agreedContent": { "type": "string" },
            "openItems": { "type": "array", "items": { "type": "string" } },
            "lastContact": { "type": "string" }
          }
        }
        """)!.AsObject();

    public async Task<CaseSummary> SummariseAsync(SummaryInput input, CancellationToken ct)
    {
        var user = input.Facts is { Count: > 0 } facts
            ? $"Bookingens data (til orientering — gentag ikke tallene):\n{string.Join("\n", facts)}\n\nNoter og mails:\n{input.Text}"
            : $"Tekst:\n{input.Text}";

        var request = new JsonObject
        {
            // No temperature: the GPT-5 models accept only the default and answer 400 to anything else.
            ["model"] = options.Value.Model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = Instructions },
                new JsonObject { ["role"] = "user", ["content"] = user },
            },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "case_summary", ["strict"] = true, ["schema"] = Schema.DeepClone() },
            },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new("Bearer", options.Value.ApiKey);

        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new SummaryUnavailableException($"OpenAI svarede {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
        var content = body?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()
            ?? throw new SummaryUnavailableException("OpenAI svarede uden et resumé.");

        var summary = JsonSerializer.Deserialize<CaseSummary>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new SummaryUnavailableException("Resuméet kunne ikke læses.");
        return summary;
    }
}

/// <summary>The summariser could not answer. Becomes a 503 with the reason.</summary>
public sealed class SummaryUnavailableException(string message) : Exception(message);
