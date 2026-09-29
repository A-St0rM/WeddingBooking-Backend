using System.ComponentModel.DataAnnotations;
using WeddingBooking.Domain.Catalogue;

namespace WeddingBooking.Api.Features.Packages;

public sealed record LineRequest(
    [Required, MaxLength(500)] string Description,
    ProductGroup Group,
    LinePricing Pricing,
    decimal UnitPrice,
    int Quantity,
    int? MinimumQuantity)
{
    public LineSpec ToSpec() => new(Description, Group, Pricing, UnitPrice, Quantity, MinimumQuantity);
}

public sealed record PackageRequest(
    [Required, MaxLength(200)] string Name,
    PackageKind Kind,
    int Year,
    [MaxLength(2000)] string? Description,
    [Required] IReadOnlyList<LineRequest> Lines);

public sealed record SetActiveRequest(bool IsActive);

public sealed record CopyPackageRequest(int Year, decimal PriceChangePercent);

public sealed record PackageLineResponse(
    Guid Id,
    string Description,
    ProductGroup Group,
    LinePricing Pricing,
    decimal UnitPrice,
    int Quantity,
    int? MinimumQuantity)
{
    public static PackageLineResponse From(PackageLine line) => new(
        line.Id, line.Description, line.Group, line.Pricing, line.UnitPrice, line.Quantity, line.MinimumQuantity);
}

public sealed record PackageResponse(
    Guid Id,
    string Name,
    PackageKind Kind,
    int Year,
    string? Description,
    bool IsActive,
    IReadOnlyList<PackageLineResponse> Lines)
{
    public static PackageResponse From(Package package) => new(
        package.Id, package.Name, package.Kind, package.Year, package.Description, package.IsActive,
        package.Lines.OrderBy(line => line.SortOrder).Select(PackageLineResponse.From).ToList());
}
