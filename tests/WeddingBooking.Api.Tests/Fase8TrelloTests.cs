using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase8TrelloTests(ApiFactory api)
{
    private HttpClient Manager => api.ClientFor(ApiFactory.ManagerEmail);

    private static MultipartFormDataContent Exports()
    {
        var form = new MultipartFormDataContent();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TrelloExports"), "*.json").Order())
        {
            var content = new ByteArrayContent(File.ReadAllBytes(file));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            form.Add(content, "files", Path.GetFileName(file));
        }
        return form;
    }

    [IntegrationFact]
    public async Task Every_card_on_the_boards_is_imported_or_reported_with_the_reason_and_a_second_run_changes_nothing()
    {
        var preview = await (await Manager.PostAsync("/api/trello/preview", Exports())).ReadAsync();
        var cards = Cards(preview);
        Assert.Equal(7, cards.Count);
        Assert.True(preview["dryRun"]!.GetValue<bool>());
        Assert.All(cards, c => Assert.True(c["action"].Str() == "Import" || c["reason"] is not null, c["cardName"].Str()));
        Assert.Equal(4, preview["importable"]!.GetValue<int>());
        Assert.Equal(3, preview["skipped"]!.GetValue<int>());
        Assert.DoesNotContain((await Manager.GetJsonAsync("/api/bookings?includePast=true")).AsArray(), b => b!["customerName"].Str() == "Anna og Mikkel");

        var import = await (await Manager.PostAsync("/api/trello/import", Exports())).ReadAsync();
        Assert.Equal(4, import["imported"]!.GetValue<int>());

        var anna = Cards(import).Single(c => c["coupleName"]?.Str() == "Anna og Mikkel");
        var booking = await Manager.GetJsonAsync($"/api/bookings/{anna["bookingId"].Str()}");
        Assert.Equal(90, booking["guestCount"]!.GetValue<int>());
        Assert.Equal("2026-06-20", booking["eventDate"].Str());
        Assert.Equal("Signed", booking["stage"].Str());
        Assert.Equal("anna@example.dk", booking["customer"]!["contacts"]![0]!["email"].Str());
        var readiness = await Manager.GetJsonAsync($"/api/bookings/{anna["bookingId"].Str()}/readiness");
        Assert.Equal("Done", readiness["items"]!.AsArray().Single(i => i!["category"].Str() == "Food")!["status"].Str());
        var note = (await Manager.GetJsonAsync($"/api/bookings/{anna["bookingId"].Str()}/notes")).AsArray().Single()!;
        Assert.Contains("faktura betalt", note["text"].Str());

        var maria = Cards(import).Single(c => c["coupleName"]?.Str() == "Maria og Jonas");
        Assert.Equal("2027-07-03", maria["eventDate"].Str());

        var again = await (await Manager.PostAsync("/api/trello/import", Exports())).ReadAsync();
        Assert.Equal(0, again["imported"]!.GetValue<int>());
        Assert.Equal(4, again["alreadyImported"]!.GetValue<int>());
    }

    private static List<JsonNode> Cards(JsonNode report) =>
        report["boards"]!.AsArray().SelectMany(b => b!["cards"]!.AsArray()).Select(c => c!).ToList();
}
