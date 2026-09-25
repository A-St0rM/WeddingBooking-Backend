using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Catalogue;

/// <summary>
/// A package on the price list for a given year. Adding it to a booking copies
/// its lines and prices onto the booking, so changing a price here never
/// changes a quote already given.
/// </summary>
public sealed class Package
{
    private readonly List<PackageLine> _lines = [];

    private Package() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PackageKind Kind { get; private set; }

    /// <summary>The season the prices apply to.</summary>
    public int Year { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Retired packages stay for the bookings that copied them, but are not offered.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyList<PackageLine> Lines => _lines;

    public static Package Create(string name, PackageKind kind, int year, string? description, IEnumerable<LineSpec> lines)
    {
        var package = new Package { Id = Guid.CreateVersion7(), IsActive = true };
        package.Update(name, kind, year, description, lines);
        return package;
    }

    public void Update(string name, PackageKind kind, int year, string? description, IEnumerable<LineSpec> lines)
    {
        if (year is < 2020 or > 2100) throw new DomainException("Året skal være et årstal.");
        var specs = lines.Select(line => line.Validated()).ToList();
        if (specs.Count == 0) throw new DomainException("En pakke skal have mindst én linje.");

        Name = Text.Required(name, "En pakke skal have et navn.");
        Kind = kind;
        Year = year;
        Description = Text.Blank(description);

        _lines.Clear();
        _lines.AddRange(specs.Select((spec, index) => PackageLine.Create(Id, spec, index)));
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>Next year's version: same lines, prices raised by a percentage and rounded to whole kroner.</summary>
    public Package CopyForYear(int year, decimal priceChangePercent)
    {
        var factor = 1 + priceChangePercent / 100m;
        return Create(
            Name.Replace(Year.ToString(), year.ToString()),
            Kind,
            year,
            Description,
            _lines.OrderBy(line => line.SortOrder)
                .Select(line => line.Spec with { UnitPrice = decimal.Round(line.UnitPrice * factor, 0, MidpointRounding.AwayFromZero) }));
    }
}

public sealed class PackageLine
{
    private PackageLine() { }

    public Guid Id { get; private set; }
    public Guid PackageId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public ProductGroup Group { get; private set; }
    public LinePricing Pricing { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public int? MinimumQuantity { get; private set; }
    public int SortOrder { get; private set; }

    public LineSpec Spec => new(Description, Group, Pricing, UnitPrice, Quantity, MinimumQuantity);

    internal static PackageLine Create(Guid packageId, LineSpec spec, int sortOrder) => new()
    {
        Id = Guid.CreateVersion7(),
        PackageId = packageId,
        Description = spec.Description,
        Group = spec.Group,
        Pricing = spec.Pricing,
        UnitPrice = spec.UnitPrice,
        Quantity = spec.Quantity,
        MinimumQuantity = spec.MinimumQuantity,
        SortOrder = sortOrder,
    };
}
