using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Trello;

public sealed record BoardSummaryResponse(string Id, string Name);

public sealed record LabelResponse(string Name, string Color);

public sealed record CheckItemResponse(string Id, string Name, bool Complete);

public sealed record ChecklistResponse(string Id, string Name, IReadOnlyList<CheckItemResponse> Items);

public sealed record CardResponse(
    string Id, string Name, string Description, DateTimeOffset? Due, bool DueComplete, string? Url,
    IReadOnlyList<LabelResponse> Labels, IReadOnlyList<ChecklistResponse> Checklists,
    // The booking this card belongs to, if any.
    Guid? BookingId);

public sealed record ListResponse(string Id, string Name, IReadOnlyList<CardResponse> Cards);

public sealed record BoardResponse(
    string Id, string Name, string? Url, DateTimeOffset FetchedAt,
    // False without a Trello key and token: the board can then be read but not changed.
    bool CanWrite,
    IReadOnlyList<ListResponse> Lists);

public sealed record CreateCardRequest([Required, MaxLength(500)] string Name, [MaxLength(10_000)] string? Description);

public sealed record MoveCardRequest([Required] string ListId);

public sealed record CheckItemRequest(bool Complete);

/// <summary>
/// Keeps each board for a short while, so a room of phones looking at it asks Trello once, and a
/// change made in Trello still shows within a minute (20 seconds here, 30 in the page's polling).
/// </summary>
public sealed class TrelloBoardCache(TimeProvider clock)
{
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, TrelloBoard Board)> boards = new();

    /// <summary>A board just read from Trello, so the next page load shows it at once.</summary>
    public void Put(string boardId, TrelloBoard board) => boards[boardId] = (clock.GetUtcNow(), board);

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
public sealed class TrelloBoardController(ITrelloGateway trello, TrelloBoardCache cache, IOptions<TrelloOptions> options, AppDbContext db, TrelloSync sync, CurrentUser currentUser) : ControllerBase
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
        return await RespondAsync(board, fetchedAt, ct);
    }

    /// <summary>A new card, straight onto Trello.</summary>
    [HttpPost("{boardId}/lists/{listId}/cards")]
    public async Task<ActionResult<BoardResponse>> CreateCard(string boardId, string listId, CreateCardRequest request, CancellationToken ct)
    {
        var board = await ConfiguredBoardAsync(boardId, ct);
        if (board?.Lists?.Any(l => l.Id == listId) != true) return NotFound();
        await trello.CreateCardAsync(listId, request.Name.Trim(), request.Description?.Trim() ?? "", ct);
        return await FreshAsync(boardId, ct);
    }

    [HttpPut("{boardId}/cards/{cardId}/list")]
    public async Task<ActionResult<BoardResponse>> MoveCard(string boardId, string cardId, MoveCardRequest request, CancellationToken ct)
    {
        var board = await ConfiguredBoardAsync(boardId, ct);
        if (board is null || board.Cards.All(c => c.Id != cardId) || board.Lists?.Any(l => l.Id == request.ListId) != true) return NotFound();
        await trello.UpdateCardAsync(cardId, null, null, request.ListId, ct);
        return await FreshAsync(boardId, ct);
    }

    /// <summary>
    /// Ticks an item on a card. An item mirroring a readiness item is changed on the booking, under the
    /// same role rules as on the booking page, and reaches Trello from there.
    /// </summary>
    [HttpPut("{boardId}/cards/{cardId}/check-items/{checkItemId}")]
    public async Task<ActionResult<BoardResponse>> Check(string boardId, string cardId, string checkItemId, CheckItemRequest request, CancellationToken ct)
    {
        var board = await ConfiguredBoardAsync(boardId, ct);
        var card = board?.Cards.FirstOrDefault(c => c.Id == cardId);
        if (card is null || card.Checklists.SelectMany(c => c.Items).All(i => i.Id != checkItemId)) return NotFound();

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.TrelloCardId == cardId, ct);
        var item = booking?.ReadinessItems.FirstOrDefault(i => i.TrelloCheckItemId == checkItemId);
        if (booking is not null && item is not null)
        {
            booking.UpdateReadiness(item.Id, request.Complete ? ReadinessStatus.Done : ReadinessStatus.InProgress, item.Notes, item.Responsible, currentUser.User);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await trello.SetCheckItemAsync(cardId, checkItemId, request.Complete, ct);
        }
        return await FreshAsync(boardId, ct);
    }

    private async Task<TrelloBoard?> ConfiguredBoardAsync(string boardId, CancellationToken ct)
    {
        if (!options.Value.BoardIds.Contains(boardId)) return null;
        if (!trello.HasKey) throw new DomainException("Boardet kan kun læses: der er ingen Trello-nøgle sat op.");
        return (await cache.GetAsync(boardId, () => trello.GetBoardAsync(boardId, ct))).Board;
    }

    /// <summary>The board as Trello has it now, with any linked booking brought in line.</summary>
    private async Task<BoardResponse> FreshAsync(string boardId, CancellationToken ct)
    {
        await sync.SyncBoardAsync(boardId, ct);
        var (board, at) = await cache.GetAsync(boardId, () => trello.GetBoardAsync(boardId, ct));
        return await RespondAsync(board, at, ct);
    }

    private async Task<BoardResponse> RespondAsync(TrelloBoard board, DateTimeOffset fetchedAt, CancellationToken ct)
    {
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
                    c.Checklists.Select(x => new ChecklistResponse(x.Id, x.Name, x.Items.Select(i => new CheckItemResponse(i.Id, i.Name, i.Complete)).ToList())).ToList(),
                    bookings.TryGetValue(c.Id, out var bookingId) ? bookingId : null))
                .ToList()))
            .ToList();

        return new BoardResponse(board.Id, board.Name, board.Url, fetchedAt, trello.HasKey, lists);
    }
}
