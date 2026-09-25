using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Bookings;

internal static class BookingQueries
{
    public static IQueryable<Booking> WithCustomer(this AppDbContext db) =>
        db.Bookings.Include(booking => booking.Customer);

    /// <summary>Live bookings on a date, other than the one given.</summary>
    public static Task<List<Booking>> OnDateAsync(this AppDbContext db, DateOnly date, Guid? except, CancellationToken ct) =>
        db.WithCustomer()
            .Where(booking => booking.EventDate == date
                && booking.Id != except
                && booking.Stage != SalesStage.Cancelled
                && booking.Stage != SalesStage.Lost)
            .OrderBy(booking => booking.CreatedAt)
            .ToListAsync(ct);
}
