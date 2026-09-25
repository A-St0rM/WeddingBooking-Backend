using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Packages;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

/// <summary>What a booking is made of. Each change answers with the whole booking and its new price.</summary>
[ApiController]
[Route("api/bookings/{id:guid}")]
[ManagementOnly]
public sealed class BookingLinesController(AppDbContext db, BookingResponder responder) : ControllerBase
{
    /// <summary>Copies a package's lines and prices onto the booking.</summary>
    [HttpPost("packages")]
    public async Task<ActionResult<BookingResponse>> AddPackage(Guid id, AddPackageRequest request, CancellationToken ct)
    {
        var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);
        if (package is null) return NotFound();
        return await ChangeAsync(id, booking => booking.AddPackage(package), ct);
    }

    [HttpPost("lines")]
    public Task<ActionResult<BookingResponse>> AddLine(Guid id, LineRequest request, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.AddLine(request.ToSpec()), ct);

    [HttpPut("lines/{lineId:guid}")]
    public Task<ActionResult<BookingResponse>> UpdateLine(Guid id, Guid lineId, LineRequest request, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.UpdateLine(lineId, request.ToSpec()), ct);

    [HttpDelete("lines/{lineId:guid}")]
    public Task<ActionResult<BookingResponse>> RemoveLine(Guid id, Guid lineId, CancellationToken ct) =>
        ChangeAsync(id, booking => booking.RemoveLine(lineId), ct);

    private async Task<ActionResult<BookingResponse>> ChangeAsync(Guid id, Action<Booking> change, CancellationToken ct)
    {
        var booking = await db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        change(booking);
        await db.SaveChangesAsync(ct);
        return await responder.RespondAsync(booking, ct);
    }
}
