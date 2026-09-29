using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Trello;

public sealed record BoardSummaryResponse(string Id, string Name);

public sealed record LabelResponse(string Name, string Color);

public sealed record CheckItemResponse(string Name, bool Complete);

public sealed record ChecklistResponse(string Name, IReadOnlyList<CheckItemResponse> Items);

public sealed record CardResponse(
    string Id, string Name, string Description, DateTimeOffset? Due, bool DueComplete, string? Url,
    IReadOnlyList<LabelResponse> Labels, IReadOnlyList<ChecklistResponse> Checklists,
    // The booking this card belongs to, if any.
    Guid? BookingId);

public sealed record ListResponse(string Id, string Name, IReadOnlyList<CardResponse> Cards);

public sealed record BoardResponse(string Id, string Name, string? Url, DateTimeOffset FetchedAt, IReadOnlyList<ListResponse> Lists);

/// <summary>
/// Keeps each board for a short while, so a room of phones looking at it asks Trello once, and a
/// change made in Trello still shows within a minute (20 seconds here, 30 in the page's polling).
/// </summary>
public sealed class TrelloBoardCache(TimeProvider clock)
{
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, TrelloBoard Board)> boards = new();

    public async Task<(TrelloBoard Board, DateTimeOffset At)> GetAsync(string boardId, Func<Task<TrelloBoard>> fetch)
    {
        var now = clock.GetUtcNow();
        if (boards.TryGetValue(boardId, out var cached) && now - cached.At < Freshness) return (cached.Board, cached.At);
        var board = await fetch();
        boards[boardId] = (now, board);
        return (board, now);
    }
}

/// <summary>
/// The Trello boards, shown inside the system as Trello shows them. Only the boards set in
/// configuration can be asked for: this is not a way to read any board through the API.
/// </summary>
[ApiController]
[Route("api/trello/boards")]
public sealed class TrelloBoardController(ITrelloGateway trello, TrelloBoardCache cache, IOptions<TrelloOptions> options, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<BoardSummaryResponse>> Boards(CancellationToken ct)
    {
        var boards = new List<BoardSummaryResponse>();
        foreach (var id in options.Value.BoardIds)
        {
            var (board, _) = await cache.GetAsync(id, () => trello.GetBoardAsync(id, ct));
            boards.Add(new(id, board.Name));
        }
        return boards;
    }

    [HttpGet("{boardId}")]
    public async Task<ActionResult<BoardResponse>> Board(string boardId, CancellationToken ct)
    {
        if (!options.Value.BoardIds.Contains(boardId)) return NotFound();
        var (board, fetchedAt) = await cache.GetAsync(boardId, () => trello.GetBoardAsync(boardId, ct));

        var cardIds = board.Cards.Select(c => c.Id).ToList();
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.TrelloCardId != null && cardIds.Contains(b.TrelloCardId))
            .ToDictionaryAsync(b => b.TrelloCardId!, b => b.Id, ct);

        // Archived lists and cards are hidden, as in Trello.
        var lists = (board.Lists ?? [])
            .Where(l => !l.Closed)
            .OrderBy(l => l.Position)
            .Select(l => new ListResponse(l.Id, l.Name, board.Cards
                .Where(c => c.ListId == l.Id && !c.Closed)
                .OrderBy(c => c.Position)
                .Select(c => new CardResponse(
                    c.Id, c.Name, c.Description, c.Due, c.DueComplete, c.Url,
                    c.Labels.Select(x => new LabelResponse(x.Name, x.Color)).ToList(),
                    c.Checklists.Select(x => new ChecklistResponse(x.Name, x.Items.Select(i => new CheckItemResponse(i.Name, i.Complete)).ToList())).ToList(),
                    bookings.TryGetValue(c.Id, out var bookingId) ? bookingId : null))
                .ToList()))
            .ToList();

        return new BoardResponse(board.Id, board.Name, board.Url, fetchedAt, lists);
    }
}
