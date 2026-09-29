using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Pricing;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Settings;

public sealed record PricingSettingsResponse(int SmallWeddingThreshold, decimal SmallWeddingSurcharge, DateTimeOffset UpdatedAt);

public sealed record PricingSettingsRequest([Range(0, 350)] int SmallWeddingThreshold, [Range(0, 1_000_000)] decimal SmallWeddingSurcharge);

/// <summary>The small-wedding surcharge. New bookings copy it; existing bookings keep theirs.</summary>
[ApiController]
[Route("api/settings/pricing")]
[PriceRolesOnly]
public sealed class PricingSettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<PricingSettingsResponse> Get(CancellationToken ct) => From(await LoadAsync(ct));

    [HttpPut, ManagementOnly]
    public async Task<PricingSettingsResponse> Update(PricingSettingsRequest request, CancellationToken ct)
    {
        var settings = await LoadAsync(ct);
        settings.Update(request.SmallWeddingThreshold, request.SmallWeddingSurcharge);
        await db.SaveChangesAsync(ct);
        return From(settings);
    }

    private async Task<PricingSettings> LoadAsync(CancellationToken ct)
    {
        var settings = await db.PricingSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = PricingSettings.Create(60, 0);
            db.PricingSettings.Add(settings);
        }
        return settings;
    }

    private static PricingSettingsResponse From(PricingSettings settings) =>
        new(settings.SmallWeddingThreshold, settings.SmallWeddingSurcharge, settings.UpdatedAt);
}
