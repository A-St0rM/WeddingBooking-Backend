using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Readiness;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Readiness;

public sealed record ReadinessItemResponse(
    Guid Id, ReadinessCategory Category, string Title, ReadinessStatus Status, string? Notes, string? Responsible,
    DateTimeOffset UpdatedAt, string? UpdatedBy,
    // Whether the caller's role may change this item. The API enforces it either way.
    bool CanEdit);

public sealed record ReadinessResponse(
    // Worked out from the items: every item before the day is done.
    bool ReadyForEvent,
    int GuestCount,
    IReadOnlyList<ReadinessItemResponse> Items);

public sealed record UpdateReadinessRequest(ReadinessStatus Status, [MaxLength(5_000)] string? Notes, [MaxLength(200)] string? Responsible);

public sealed record AddReadinessRequest(ReadinessCategory Category, [Required, MaxLength(200)] string Title);

/// <summary>
/// Getting a wedding ready — what the green fields on the Trello card do today.
/// Every role reads it; each role changes its own part.
/// </summary>
[ApiController]
[Route("api/bookings/{id:guid}/readiness")]
public sealed class ReadinessController(AppDbContext db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReadinessResponse>> Get(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        // Bookings made before readiness existed get their items the first time they are read.
        if (booking.EnsureReadiness()) await db.SaveChangesAsync(ct);
        return Respond(booking);
    }

    [HttpPut("{itemId:guid}")]
    public Task<ActionResult<ReadinessResponse>> Update(Guid id, Guid itemId, UpdateReadinessRequest request, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.UpdateReadiness(itemId, request.Status, request.Notes, request.Responsible, currentUser.User), ct);

    [HttpPost, ManagementOnly]
    public Task<ActionResult<ReadinessResponse>> Add(Guid id, AddReadinessRequest request, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.AddReadinessItem(request.Category, request.Title), ct);

    [HttpDelete("{itemId:guid}"), ManagementOnly]
    public Task<ActionResult<ReadinessResponse>> Remove(Guid id, Guid itemId, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.RemoveReadinessItem(itemId), ct);

    private async Task<ActionResult<ReadinessResponse>> ChangeAsync(Guid id, Action<Booking> change, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        booking.EnsureReadiness();
        change(booking);
        await db.SaveChangesAsync(ct);
        return Respond(booking);
    }

    private ReadinessResponse Respond(Booking booking)
    {
        var role = currentUser.User.Role;
        var items = booking.ReadinessItems
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Title)
            .Select(item => new ReadinessItemResponse(
                item.Id, item.Category, item.Title, item.Status, item.Notes, item.Responsible, item.UpdatedAt, item.UpdatedBy,
                ReadinessRules.CanEdit(role, item.Category)))
            .ToList();
        return new ReadinessResponse(ReadinessRules.AllDone(booking.ReadinessItems), booking.GuestCount, items);
    }
}
