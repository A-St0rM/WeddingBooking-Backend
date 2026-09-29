using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WeddingBooking.Api.Features.Trello;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase15TrelloSyncTests(ApiFactory api)
{
    private const string Secret = "app-secret";
    private const string Callback = "https://api.test/api/trello/webhook";

    [IntegrationFact]
    public async Task A_card_made_in_the_system_is_on_trello_and_one_made_in_trello_is_in_the_system()
    {
        var (client, trello, _, clock) = Start();
        var id = await BookingAsync(client, "Ronja og Emil", "2027-06-19", 80);

        var booking = await client.PostJsonAsync($"/api/bookings/{id}/trello-card");

        var card = trello.Card(booking["trelloCardId"].Str());
        Assert.Equal("b27-l1", card["idList"].Str()); // the board for its year, the first list
        Assert.Equal("Ronja og Emil · 80 pax", card["name"].Str());
        Assert.Contains("Gæster: 80", card["desc"].Str());
        var mirror = Assert.Single(trello.ChecklistsOf(card["id"].Str()));
        Assert.Equal(TrelloSyncRules.ReadinessChecklist, mirror["name"].Str());
        var readiness = await client.GetJsonAsync($"/api/bookings/{id}/readiness");
        Assert.Equal(readiness["items"]!.AsArray().Count, mirror["checkItems"]!.AsArray().Count);

        // A card made on the board page lands on Trello; one made in Trello shows on the board page.
        await client.PostJsonAsync("/api/trello/boards/b27/lists/b27-l2/cards", new { name = "Bestil telt", description = "" });
        Assert.Contains(trello.Cards, c => c["name"].Str() == "Bestil telt" && c["idList"].Str() == "b27-l2");
        trello.AddCard("b27-l1", "Lavet i Trello");
        clock.Advance(TrelloBoardCache.Freshness + TimeSpan.FromSeconds(1));
        var board = await client.GetJsonAsync("/api/trello/boards/b27");
        Assert.True(board["canWrite"]!.GetValue<bool>());
        Assert.Contains(Cards(board), c => c["name"].Str() == "Lavet i Trello");
        Assert.Equal(id, Cards(board).Single(c => c["id"].Str() == card["id"].Str())["bookingId"].Str());
    }

    [IntegrationFact]
    public async Task A_tick_in_one_place_is_a_tick_in_the_other()
    {
        var (client, trello, factory, _) = Start();
        var id = await BookingAsync(client, "Sigrid og Aksel", "2027-06-26", 70);
        var cardId = (await client.PostJsonAsync($"/api/bookings/{id}/trello-card"))["trelloCardId"].Str();
        var items = (await client.GetJsonAsync($"/api/bookings/{id}/readiness"))["items"]!.AsArray().Select(i => i!).ToList();

        // Ticked in the system → ticked on Trello.
        await client.PutJsonAsync($"/api/bookings/{id}/readiness/{items[0]["id"].Str()}", new { status = "Done", notes = (string?)null, responsible = (string?)null });
        Assert.True(trello.IsComplete(cardId, items[0]["title"].Str()));

        // Ticked in Trello → ticked in the system, once Trello's webhook arrives.
        trello.Tick(cardId, items[1]["title"].Str());
        var body = """{"model":{"id":"b27"},"action":{"type":"updateCheckItemStateOnCard"}}""";
        var webhook = factory.CreateClient();
        using (var request = Signed(body))
        {
            Assert.Equal(HttpStatusCode.OK, (await webhook.SendAsync(request)).StatusCode);
        }
        var item = await ItemAsync(client, id, items[1]["id"].Str());
        Assert.Equal("Done", item["status"].Str());
        Assert.Equal("Trello", item["updatedBy"].Str());

        // Ticked on the board page → ticked in the system and on Trello.
        var board = await client.GetJsonAsync("/api/trello/boards/b27");
        var checkItem = Cards(board).Single(c => c["id"].Str() == cardId)["checklists"]!.AsArray()
            .SelectMany(c => c!["items"]!.AsArray()).Single(i => i!["name"].Str() == items[2]["title"].Str())!;
        await client.PutJsonAsync($"/api/trello/boards/b27/cards/{cardId}/check-items/{checkItem["id"].Str()}", new { complete = true });
        Assert.Equal("Done", (await ItemAsync(client, id, items[2]["id"].Str()))["status"].Str());
        Assert.True(trello.IsComplete(cardId, items[2]["title"].Str()));
    }

    [IntegrationFact]
    public async Task An_unsigned_webhook_is_refused()
    {
        var (_, _, factory, _) = Start();
        var body = """{"model":{"id":"b27"}}""";
        using var request = Signed(body);
        request.Headers.Remove("X-Trello-Webhook");
        request.Headers.Add("X-Trello-Webhook", Convert.ToBase64String(new byte[20]));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().SendAsync(request)).StatusCode);
    }

    [IntegrationFact]
    public async Task A_changed_guest_count_reaches_the_card_without_anyone_typing_it()
    {
        var (client, trello, _, _) = Start();
        var id = await BookingAsync(client, "Karla og Storm", "2027-08-07", 60);
        var booking = await client.PostJsonAsync($"/api/bookings/{id}/trello-card");

        await client.PutJsonAsync($"/api/bookings/{id}", new
        {
            eventDate = "2027-08-07",
            guestCount = 95,
            paymentTerms = booking["paymentTerms"]!.GetValue<string>(),
            version = booking["version"]!.GetValue<uint>(),
        });

        var card = trello.Card(booking["trelloCardId"].Str());
        Assert.Equal("Karla og Storm · 95 pax", card["name"].Str());
        Assert.Contains("Gæster: 95", card["desc"].Str());
        Assert.DoesNotContain("Gæster: 60", card["desc"].Str());
    }

    [IntegrationFact]
    public async Task A_lost_webhook_is_made_good_by_the_reconciliation()
    {
        var (client, trello, factory, _) = Start();
        var id = await BookingAsync(client, "Ebba og Holger", "2027-08-14", 50);
        var cardId = (await client.PostJsonAsync($"/api/bookings/{id}/trello-card"))["trelloCardId"].Str();
        var item = (await client.GetJsonAsync($"/api/bookings/{id}/readiness"))["items"]!.AsArray()[0]!;

        trello.Tick(cardId, item["title"].Str()); // and Trello never calls

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TrelloSync>().ReconcileAsync(CancellationToken.None);
        }
        Assert.Equal("Done", (await ItemAsync(client, id, item["id"].Str()))["status"].Str());
        Assert.Equal(TimeSpan.FromMinutes(5), TrelloReconciler.Interval);
    }

    private static IEnumerable<JsonNode> Cards(JsonNode board) =>
        board["lists"]!.AsArray().SelectMany(l => l!["cards"]!.AsArray()).Select(c => c!);

    private static async Task<string> BookingAsync(HttpClient client, string couple, string date, int guests) =>
        (await client.PostJsonAsync("/api/bookings", new { partners = Json.Couple(couple), eventDate = date, guestCount = guests }))["id"].Str();

    private static async Task<JsonNode> ItemAsync(HttpClient client, string bookingId, string itemId) =>
        (await client.GetJsonAsync($"/api/bookings/{bookingId}/readiness"))["items"]!.AsArray().Single(i => i!["id"].Str() == itemId)!;

    private static HttpRequestMessage Signed(string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/trello/webhook") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        var signature = HMACSHA1.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body + Callback));
        request.Headers.Add("X-Trello-Webhook", Convert.ToBase64String(signature));
        return request;
    }

    private (HttpClient Client, FakeTrelloApi Trello, WebApplicationFactory<Program> Factory, MovableClock Clock) Start()
    {
        var trello = new FakeTrelloApi();
        var clock = new MovableClock();
        var factory = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Trello:BoardIds:0", "b26");
            builder.UseSetting("Trello:BoardIds:1", "b27");
            builder.UseSetting("Trello:ApiKey", "test-key");
            builder.UseSetting("Trello:Token", "test-token");
            builder.UseSetting("Trello:AppSecret", Secret);
            builder.UseSetting("Trello:CallbackUrl", Callback);
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<ITrelloGateway, TrelloApiGateway>().ConfigurePrimaryHttpMessageHandler(() => trello);
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.TokenFor(ApiFactory.ManagerEmail));
        return (client, trello, factory, clock);
    }

    /// <summary>Trello's REST API, kept in memory: two boards with two lists each.</summary>
    private sealed class FakeTrelloApi : HttpMessageHandler
    {
        private readonly List<JsonObject> lists = [];
        private readonly List<JsonObject> checklists = [];
        private int next;

        public List<JsonObject> Cards { get; } = [];

        public FakeTrelloApi()
        {
            foreach (var board in new[] { "b26", "b27" })
            {
                lists.Add(new() { ["id"] = $"{board}-l1", ["idBoard"] = board, ["name"] = "Forespørgsler", ["pos"] = 1, ["closed"] = false });
                lists.Add(new() { ["id"] = $"{board}-l2", ["idBoard"] = board, ["name"] = "Bekræftede", ["pos"] = 2, ["closed"] = false });
            }
        }

        public JsonObject Card(string id) => Cards.Single(c => c["id"].Str() == id);

        public IEnumerable<JsonObject> ChecklistsOf(string cardId) => checklists.Where(c => c["idCard"].Str() == cardId);

        public JsonObject AddCard(string listId, string name)
        {
            var card = new JsonObject
            {
                ["id"] = $"card{++next}-{Guid.NewGuid():N}", ["idList"] = listId, ["name"] = name, ["desc"] = "", ["pos"] = next, ["closed"] = false,
                ["idBoard"] = lists.Single(l => l["id"].Str() == listId)["idBoard"].Str(),
            };
            Cards.Add(card);
            return card;
        }

        public bool IsComplete(string cardId, string name) => CheckItem(cardId, name)["state"].Str() == "complete";

        public void Tick(string cardId, string name) => CheckItem(cardId, name)["state"] = "complete";

        private JsonObject CheckItem(string cardId, string name) =>
            ChecklistsOf(cardId).SelectMany(c => c["checkItems"]!.AsArray()).Select(i => i!.AsObject()).Single(i => i["name"].Str() == name);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            Assert.Equal("api.trello.com", uri.Host);
            var query = HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal("test-token", query["token"]);
            var path = uri.AbsolutePath["/1/".Length..].Split('/');

            JsonNode? answer = (request.Method.Method, path) switch
            {
                ("GET", ["boards", var board]) => Board(board),
                ("POST", ["cards"]) => AddCard(query["idList"]!, query["name"]!).Also(c => c["desc"] = query["desc"]),
                ("PUT", ["cards", var id]) => Card(id).Also(c =>
                {
                    foreach (var field in new[] { "name", "desc", "idList" }) if (query[field] is { } value) c[field] = value;
                }),
                ("POST", ["cards", var id, "checklists"]) => Add(checklists, new() { ["id"] = $"cl{++next}", ["idCard"] = id, ["name"] = query["name"], ["checkItems"] = new JsonArray() }),
                ("POST", ["checklists", var id, "checkItems"]) => Add(checklists.Single(c => c["id"].Str() == id)["checkItems"]!.AsArray(),
                    new() { ["id"] = $"ci{++next}", ["name"] = query["name"], ["state"] = query["checked"] == "true" ? "complete" : "incomplete" }),
                ("PUT", ["cards", var id, "checkItem", var item]) => ChecklistsOf(id).SelectMany(c => c["checkItems"]!.AsArray())
                    .Single(i => i!["id"].Str() == item)!.AsObject().Also(i => i["state"] = query["state"]),
                ("POST", ["webhooks"]) => new JsonObject { ["id"] = "wh" },
                _ => null,
            };
            return Task.FromResult(answer is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString(), Encoding.UTF8, "application/json") });
        }

        private JsonObject Board(string id)
        {
            var cards = Cards.Where(c => c["idBoard"].Str() == id).ToList();
            var ids = cards.Select(c => c["id"].Str()).ToHashSet();
            return new JsonObject
            {
                ["id"] = id, ["name"] = id == "b26" ? "Bryllup 26" : "Bryllup 27",
                ["lists"] = new JsonArray(lists.Where(l => l["idBoard"].Str() == id).Select(l => (JsonNode)l.DeepClone()).ToArray()),
                ["cards"] = new JsonArray(cards.Select(c => (JsonNode)c.DeepClone()).ToArray()),
                ["checklists"] = new JsonArray(checklists.Where(c => ids.Contains(c["idCard"].Str())).Select(c => (JsonNode)c.DeepClone()).ToArray()),
            };
        }

        private static JsonObject Add(List<JsonObject> into, JsonObject item)
        {
            into.Add(item);
            return item;
        }

        private static JsonObject Add(JsonArray into, JsonObject item)
        {
            into.Add(item);
            return (JsonObject)item.DeepClone();
        }
    }

    private sealed class MovableClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject Also(this JsonObject node, Action<JsonObject> change)
    {
        change(node);
        return node;
    }
}
