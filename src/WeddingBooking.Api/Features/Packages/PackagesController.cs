using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Packages;

/// <summary>The price list. Every package carries prices, so only the price roles see it.</summary>
[ApiController]
[Route("api/packages")]
[PriceRolesOnly]
public sealed class PackagesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<PackageResponse>> List([FromQuery] int? year, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.Packages.AsNoTracking();
        if (year is { } y) query = query.Where(p => p.Year == y);
        if (!includeInactive) query = query.Where(p => p.IsActive);
        var packages = await query.OrderByDescending(p => p.Year).ThenBy(p => p.Kind).ThenBy(p => p.Name).ToListAsync(ct);
        return packages.Select(PackageResponse.From).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PackageResponse>> Get(Guid id, CancellationToken ct)
    {
        var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return package is null ? NotFound() : PackageResponse.From(package);
    }

    [HttpPost, ManagementOnly]
    public async Task<PackageResponse> Create(PackageRequest request, CancellationToken ct)
    {
        var package = Package.Create(request.Name, request.Kind, request.Year, request.Description, request.Lines.Select(l => l.ToSpec()));
        db.Packages.Add(package);
        await db.SaveChangesAsync(ct);
        return PackageResponse.From(package);
    }

    [HttpPut("{id:guid}"), ManagementOnly]
    public Task<ActionResult<PackageResponse>> Update(Guid id, PackageRequest request, CancellationToken ct) =>
        ChangeAsync(id, p => p.Update(request.Name, request.Kind, request.Year, request.Description, request.Lines.Select(l => l.ToSpec())), ct);

    [HttpPut("{id:guid}/active"), ManagementOnly]
    public Task<ActionResult<PackageResponse>> SetActive(Guid id, SetActiveRequest request, CancellationToken ct) =>
        ChangeAsync(id, p => p.SetActive(request.IsActive), ct);

    /// <summary>Next season's version of a package, prices raised by a percentage.</summary>
    [HttpPost("{id:guid}/copy"), ManagementOnly]
    public async Task<ActionResult<PackageResponse>> Copy(Guid id, CopyPackageRequest request, CancellationToken ct)
    {
        var source = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (source is null) return NotFound();
        var copy = source.CopyForYear(request.Year, request.PriceChangePercent);
        db.Packages.Add(copy);
        await db.SaveChangesAsync(ct);
        return PackageResponse.From(copy);
    }

    private async Task<ActionResult<PackageResponse>> ChangeAsync(Guid id, Action<Package> change, CancellationToken ct)
    {
        var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (package is null) return NotFound();
        change(package);
        await db.SaveChangesAsync(ct);
        return PackageResponse.From(package);
    }
}
