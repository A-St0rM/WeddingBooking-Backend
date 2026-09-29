using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Infrastructure.Persistence;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Trello;

/// <summary>
/// Keeps the bookings and their Trello cards agreeing. The booking's date and guest count are the
/// system's and are written to the card; the readiness items and the card's mirror checklist can be
/// ticked on either side. One path for all of it: a change in the system, a webhook from Trello and
/// the periodic reconciliation all end in <see cref="SyncBoardAsync"/>.
/// </summary>
public sealed class TrelloSync(AppDbContext db, ITrelloGateway trello, TrelloBoardCache cache, IOptions<TrelloOptions> options, ILogger<TrelloSync> logger)
{
    public bool CanWrite => trello.HasKey;

    public IReadOnlyList<string> BoardIds => options.Value.BoardIds;

    /// <summary>Reads the board fresh, keeps it for the page, and brings every linked booking in line.</summary>
    public async Task<TrelloBoard> SyncBoardAsync(string boardId, CancellationToken ct)
    {
        var board = await trello.GetBoardAsync(boardId, ct);
        cache.Put(boardId, board);
        if (!CanWrite) return board;

        var cards = board.Cards.Where(c => !c.Closed).ToDictionary(c => c.Id);
        var ids = cards.Keys.ToList();
        var bookings = await db.Bookings.Include(b => b.Customer).Where(b => b.TrelloCardId != null && ids.Contains(b.TrelloCardId)).ToListAsync(ct);
        foreach (var booking in bookings) await SyncCardAsync(booking, cards[booking.TrelloCardId!], ct);
        await db.SaveChangesAsync(ct);

        // The card may just have changed; read it again for the page.
        if (bookings.Count > 0) cache.Put(boardId, await trello.GetBoardAsync(boardId, ct));
        return board;
    }

    /// <summary>
    /// After a change in the system: finds the board with the booking's card and syncs it. Best effort —
    /// if Trello does not answer, the reconciliation catches up within minutes.
    /// </summary>
    public async Task SyncBookingAsync(Booking booking, CancellationToken ct)
    {
        if (!CanWrite || booking.TrelloCardId is null) return;
        try
        {
            foreach (var boardId in BoardIds)
            {
                var (board, _) = await cache.GetAsync(boardId, () => trello.GetBoardAsync(boardId, ct));
                if (board.Cards.Any(c => c.Id == booking.TrelloCardId))
                {
                    await SyncBoardAsync(boardId, ct);
                    return;
                }
            }
        }
        catch (Exception error) when (error is DomainException or HttpRequestException)
        {
            logger.LogWarning(error, "Could not sync booking {BookingId} to Trello; the reconciliation will retry.", booking.Id);
        }
    }

    /// <summary>A card for a booking that has none: on the board for its year, in the first list.</summary>
    public async Task CreateCardForBookingAsync(Booking booking, CancellationToken ct)
    {
        if (booking.TrelloCardId is not null) throw new DomainException("Bookingen har allerede et Trello-kort.");
        if (BoardIds.Count == 0) throw new DomainException("Der er ikke sat Trello-boards op.");

        var year = (booking.EventDate.Year % 100).ToString("00");
        TrelloBoard? chosen = null;
        foreach (var boardId in BoardIds)
        {
            var (board, _) = await cache.GetAsync(boardId, () => trello.GetBoardAsync(boardId, ct));
            chosen ??= board;
            if (board.Name.Contains(year, StringComparison.Ordinal)) { chosen = board; break; }
        }

        var list = chosen!.Lists?.Where(l => !l.Closed).OrderBy(l => l.Position).FirstOrDefault()
            ?? throw new DomainException($"Boardet {chosen.Name} har ingen lister.");
        var (name, description) = TrelloSyncRules.WithBooking($"{booking.Customer.Name} · {booking.GuestCount} pax", "", booking.EventDate, booking.GuestCount);
        booking.LinkTrelloCard(await trello.CreateCardAsync(list.Id, name, description, ct));
        await db.SaveChangesAsync(ct);
        await SyncBoardAsync(chosen.Id, ct);
    }

    /// <summary>Every configured board, one at a time. What a lost webhook would have done, done anyway.</summary>
    public async Task ReconcileAsync(CancellationToken ct)
    {
        foreach (var boardId in BoardIds)
        {
            try
            {
                await SyncBoardAsync(boardId, ct);
            }
            catch (Exception error) when (error is DomainException or HttpRequestException)
            {
                logger.LogWarning(error, "Reconciling Trello board {BoardId} failed; next round will retry.", boardId);
            }
        }
    }

    private async Task SyncCardAsync(Booking booking, TrelloCard card, CancellationToken ct)
    {
        var (name, description) = TrelloSyncRules.WithBooking(card.Name, card.Description, booking.EventDate, booking.GuestCount);
        if (name != card.Name || description != card.Description)
        {
            await trello.UpdateCardAsync(card.Id, name != card.Name ? name : null, description != card.Description ? description : null, null, ct);
        }

        var checklist = card.Checklists.FirstOrDefault(c => c.Name == TrelloSyncRules.ReadinessChecklist);
        var checklistId = checklist?.Id;
        foreach (var item in booking.ReadinessItems.OrderBy(i => i.Category))
        {
            var mirror = checklist?.Items.FirstOrDefault(i => i.Id == item.TrelloCheckItemId);
            switch (TrelloSyncRules.Decide(item.IsDone, mirror?.Complete, mirror is null ? null : item.TrelloSyncedDone))
            {
                case CheckItemAction.AddToTrello:
                    checklistId ??= await trello.CreateChecklistAsync(card.Id, TrelloSyncRules.ReadinessChecklist, ct);
                    item.RecordTrelloSync(await trello.AddCheckItemAsync(checklistId, item.Title, item.IsDone, ct), item.IsDone);
                    break;
                case CheckItemAction.PushToTrello:
                    await trello.SetCheckItemAsync(card.Id, mirror!.Id, item.IsDone, ct);
                    item.RecordTrelloSync(mirror.Id, item.IsDone);
                    break;
                case CheckItemAction.TakeFromTrello:
                    item.TakeTrelloState(mirror!.Complete);
                    break;
                default:
                    if (item.TrelloSyncedDone != mirror!.Complete) item.RecordTrelloSync(mirror.Id, mirror.Complete);
                    break;
            }
        }
    }
}

/// <summary>Reconciles every five minutes, so a webhook Trello lost is made good without anyone doing anything.</summary>
public sealed class TrelloReconciler(IServiceScopeFactory scopes, ILogger<TrelloReconciler> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<TrelloSync>();
                if (sync.CanWrite && sync.BoardIds.Count > 0) await sync.ReconcileAsync(stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Trello reconciliation failed.");
            }
        }
    }
}
