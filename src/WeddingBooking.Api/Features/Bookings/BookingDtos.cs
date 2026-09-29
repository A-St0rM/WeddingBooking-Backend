using System.ComponentModel.DataAnnotations;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Domain.Bookings;

namespace WeddingBooking.Api.Features.Bookings;

public sealed record BookingSummaryResponse(
    Guid Id,
    DateOnly EventDate,
    int GuestCount,
    Guid CustomerId,
    string CustomerName,
    SalesStage Stage,
    PaymentTerms PaymentTerms)
{
    public static BookingSummaryResponse From(Booking booking) => new(
        booking.Id, booking.EventDate, booking.GuestCount, booking.CustomerId, booking.Customer.Name,
        booking.Stage, booking.PaymentTerms);
}

public sealed record BookingResponse(
    Guid Id,
    DateOnly EventDate,
    int GuestCount,
    PaymentTerms PaymentTerms,
    SalesStage Stage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // Send back unchanged when saving; a stale value means someone else saved first.
    uint Version,
    CustomerResponse Customer,
    // Other live bookings on the same date. A warning, never a block.
    IReadOnlyList<BookingSummaryResponse> SameDateBookings);

public sealed record CreateBookingRequest(
    // An existing customer. Leave empty and give CustomerName to create one.
    Guid? CustomerId,
    [MaxLength(200)] string? CustomerName,
    [Required] DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    PaymentTerms? PaymentTerms);

public sealed record UpdateBookingRequest(
    [Required] DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    PaymentTerms PaymentTerms,
    uint Version);

public sealed record ChangeStageRequest(SalesStage Stage);
