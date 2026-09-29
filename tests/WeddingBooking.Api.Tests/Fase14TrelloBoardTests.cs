using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WeddingBooking.Api.Features.Trello;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Fase14TrelloBoardTests(ApiFactory api)
{
    private static readonly string Export = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TrelloExports", "bryllup-27.json"));

    [IntegrationFact]
    public async Task The_board_is_read_without_a_key_through_the_api_with_its_lists_cards_labels_and_checklists()
    {
        var (client, trello, _) = Start();

        var board = await client.GetJsonAsync("/api/trello/boards/b27");

        Assert.Equal("Bryllup 27", board["name"].Str());
        Assert.Equal(["Forespørgsler", "Bekræftede bryllupper"], board["lists"]!.AsArray().Select(l => l!["name"].Str()));
        var cards = board["lists"]!.AsArray().SelectMany(l => l!["cards"]!.AsArray()).Select(c => c!["name"].Str()).ToList();
        Assert.Contains("Maria og Jonas - 120 gæster - 3. juli", cards);
        Assert.DoesNotContain("Gammelt kort 70 pax 1.5.2027", cards); // archived, as in Trello

        var requested = Assert.Single(trello.Requests);
        Assert.Equal("https://trello.com/b/b27.json", requested); // the open address, no key
    }

    [IntegrationFact]
    public async Task A_change_in_trello_shows_within_a_minute()
    {
        var (client, trello, clock) = Start();
        await client.GetJsonAsync("/api/trello/boards/b27");

        trello.Json = Export.Replace("Maria og Jonas - 120 gæster - 3. juli", "Maria og Jonas - 124 gæster - 3. juli");
        clock.Advance(TrelloBoardCache.Freshness + TimeSpan.FromSeconds(1));
        var board = await client.GetJsonAsync("/api/trello/boards/b27");

        Assert.Contains(board["lists"]!.AsArray().SelectMany(l => l!["cards"]!.AsArray()), c => c!["name"].Str().Contains("124 gæster"));
    }

    [IntegrationFact]
    public async Task Only_the_configured_boards_can_be_read()
    {
        var (client, _, _) = Start();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/trello/boards/someone-elses-board")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_card_imported_as_a_booking_links_to_it()
    {
        var (client, _, _) = Start();
        using (var form = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(Export));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            form.Add(file, "files", "bryllup-27.json");
            await (await client.PostAsync("/api/trello/import", form)).ReadAsync();
        }

        var card = (await client.GetJsonAsync("/api/trello/boards/b27"))["lists"]!.AsArray()
            .SelectMany(l => l!["cards"]!.AsArray()).Single(c => c!["name"].Str().StartsWith("Maria og Jonas"))!;
        var booking = await client.GetJsonAsync($"/api/bookings/{card["bookingId"].Str()}");
        Assert.Equal(card["id"].Str(), booking["trelloCardId"].Str());
    }

    private (HttpClient Client, FakeTrello Trello, MovableClock Clock) Start()
    {
        var trello = new FakeTrello(Export);
        var clock = new MovableClock();
        var factory = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Trello:BoardIds:0", "b27");
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<ITrelloGateway, TrelloApiGateway>().ConfigurePrimaryHttpMessageHandler(() => trello);
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.TokenFor(ApiFactory.ManagerEmail));
        return (client, trello, clock);
    }

    private sealed class FakeTrello(string json) : HttpMessageHandler
    {
        public string Json { get; set; } = json;
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class MovableClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
}
