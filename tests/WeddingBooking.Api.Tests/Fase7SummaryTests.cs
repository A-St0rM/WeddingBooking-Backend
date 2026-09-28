using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Summaries;
using WeddingBooking.Integrations.Summaries;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase7SummaryTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    [IntegrationFact]
    public async Task The_standalone_summary_needs_no_login()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/summary/standalone",
            new { text = "Hej Johan. Vi vender tilbage efter nytår om morgenmaden. Kan vi få en avec til kaffen?" });
        var summary = await response.ReadAsync();

        Assert.Contains("morgenmaden", string.Join(" ", summary["summary"]!["openItems"]!.AsArray().Select(i => i.Str())));
        Assert.Null(summary["facts"]);
    }

    [IntegrationFact]
    public async Task A_booking_summary_takes_its_numbers_from_the_booking_and_is_not_stored()
    {
        var pkg = (await Manager.GetJsonAsync("/api/packages?year=2026")).AsArray().First(p => p!["kind"].Str() == "Base")!;
        var booking = await Manager.PostJsonAsync("/api/bookings", new { partners = Json.Couple("Resumé Par"), eventDate = "2028-06-17", guestCount = 94 });
        var id = booking["id"].Str();
        await Manager.PostJsonAsync($"/api/bookings/{id}/packages", new { packageId = pkg["id"].Str() });
        await Manager.PostJsonAsync($"/api/bookings/{id}/notes", new { type = "Email", text = "Vi bliver nok omkring 80 gæster. Vi vender tilbage om morgenmad." });

        var summary = await Manager.PostJsonAsync($"/api/bookings/{id}/summary", new { extraText = (string?)null });
        var facts = summary["facts"]!.AsArray().Select(f => f.Str()).ToList();

        Assert.Contains("Gæster: 94", facts);
        Assert.Contains(facts, f => f.StartsWith("Dato: lørdag den 17. juni 2028"));
        Assert.Contains(facts, f => f == $"Pris i alt: {94 * 1495:N0} kr.".Replace(",", "."));
        Assert.Single((await Manager.GetJsonAsync($"/api/bookings/{id}/notes")).AsArray());
    }

    [Fact]
    public async Task The_openai_adapter_asks_for_four_fields_leaves_numbers_out_and_reads_the_answer()
    {
        var handler = new StubHandler("""{"status":"Afventer svar","agreedContent":"Middag","openItems":["Morgenmad — afventer svar"],"lastContact":"September"}""");
        var summariser = new OpenAiSummariser(new HttpClient(handler), Options.Create(new OpenAiOptions { ApiKey = "test", Model = "test-model" }));

        var summary = await summariser.SummariseAsync(new SummaryInput(["Gæster: 94"], "Vi bliver 80."), CancellationToken.None);

        Assert.Equal(["Morgenmad — afventer svar"], summary.OpenItems);
        var sent = JsonNode.Parse(handler.Body!)!;
        Assert.Equal("json_schema", sent["response_format"]!["type"]!.GetValue<string>());
        Assert.Contains("Nævn ikke gæsteantal", sent["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("Bearer test", handler.Authorization);
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            Authorization = request.Headers.Authorization?.ToString();
            var answer = new JsonObject { ["choices"] = new JsonArray { new JsonObject { ["message"] = new JsonObject { ["content"] = content } } } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(answer) };
        }
    }
}
