using System.ComponentModel.DataAnnotations;
using WeddingBooking.Api.Features.Customers;
using WeddingBooking.Api.Features.Packages;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Catalogue;

namespace WeddingBooking.Api.Features.Bookings;

public sealed record BookingSummaryResponse(
    Guid Id,
    DateOnly EventDate,
    int GuestCount,
    Guid CustomerId,
    string CustomerName,
    SalesStage Stage,
    DisplayStatus Status,
    PaymentTerms PaymentTerms)
{
    public static BookingSummaryResponse From(Booking booking, DisplayStatus status) => new(
        booking.Id, booking.EventDate, booking.GuestCount, booking.CustomerId, booking.Customer.Name,
        booking.Stage, status, booking.PaymentTerms);
}

public sealed record BookingResponse(
    Guid Id,
    DateOnly EventDate,
    int GuestCount,
    PaymentTerms PaymentTerms,
    SalesStage Stage,
    BookingOrigin Origin,
    BookingKind Kind,
    // The one word staff see, worked out from the stage, the payments and the readiness.
    DisplayStatus Status,
    DateTimeOffset? SignedAt,
    DateTimeOffset? ViewingAt,
    ViewingHostResponse? ViewingHost,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // Send back unchanged when saving; a stale value means someone else saved first.
    uint Version,
    CustomerResponse Customer,
    // What was agreed, without prices — the kitchen reads the menu from here.
    IReadOnlyList<BookingLineResponse> Lines,
    // Worked out on every read. Null for the roles that do not see prices.
    PriceResponse? Price,
    // Other live bookings on the same date. A warning, never a block.
    IReadOnlyList<BookingSummaryResponse> SameDateBookings);

public sealed record BookingLineResponse(
    Guid Id,
    string Description,
    ProductGroup Group,
    LinePricing Pricing,
    int Quantity,
    int? MinimumQuantity,
    string? SourcePackageName)
{
    public static BookingLineResponse From(BookingLine line) => new(
        line.Id, line.Description, line.Group, line.Pricing, line.Quantity, line.MinimumQuantity, line.SourcePackageName);
}

public sealed record PricedLineResponse(Guid LineId, decimal UnitPrice, int Units, decimal Total);

public sealed record SurchargeResponse(int Threshold, decimal Amount, bool Applies);

public sealed record PriceResponse(IReadOnlyList<PricedLineResponse> Lines, decimal Subtotal, SurchargeResponse Surcharge, decimal Total);

public sealed record AddPackageRequest(Guid PackageId);

public sealed record CreateBookingRequest(
    // An existing customer. Leave empty and give the two people to create one.
    Guid? CustomerId,
    [MaxLength(2)] IReadOnlyList<PartnerRequest>? Partners,
    [Required] DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    PaymentTerms? PaymentTerms);

public sealed record UpdateBookingRequest(
    [Required] DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    PaymentTerms PaymentTerms,
    uint Version);

public sealed record ChangeStageRequest(SalesStage Stage);

public sealed record ViewingHostResponse(Guid Id, string Name, string? Phone);

public sealed record ConfirmAsWeddingRequest(
    [Required] DateOnly EventDate,
    [Range(1, Booking.MaxGuests)] int GuestCount,
    PaymentTerms PaymentTerms,
    Guid PayerId);

public sealed record DeclineRequest([System.ComponentModel.DataAnnotations.MaxLength(2000)] string? Reason);

public sealed record ScheduleViewingRequest(DateTimeOffset? At, Guid? HostId);
