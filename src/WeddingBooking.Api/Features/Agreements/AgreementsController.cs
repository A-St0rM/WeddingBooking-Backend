using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Api.Features.Payments;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Agreements;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Catalogue;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Files;
using WeddingBooking.Domain.Payments;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Agreements;

public sealed record AgreementVersionResponse(
    Guid Id, int Number, DateTimeOffset GeneratedAt, string GeneratedBy, string? Clarification, DateTimeOffset? SignedAt, string? SignedUploadedBy);

public sealed record AgreementResponse(string? Clarification, IReadOnlyList<AgreementVersionResponse> Versions);

public sealed record ClarificationRequest([MaxLength(10_000)] string? Clarification);

/// <summary>
/// The agreement confirmation: generated from the booking plus Johan's
/// clarification, every version kept, and the signed copy uploaded when it
/// comes back by mail. The upload is what makes a booking "Underskrevet".
/// </summary>
[ApiController]
[Route("api/bookings/{id:guid}/agreement")]
[PriceRolesOnly]
public sealed class AgreementsController(
    AppDbContext db, IAgreementRenderer renderer, IFileStore files, IAccountingSystem accounting,
    BookingFinance finance, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    private const int MaxUploadBytes = 15 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<AgreementResponse>> Get(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        return booking is null ? NotFound() : await ResponseAsync(booking, ct);
    }

    [HttpPut("clarification"), ManagementOnly]
    public async Task<ActionResult<AgreementResponse>> SetClarification(Guid id, ClarificationRequest request, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        booking.SetAgreementClarification(request.Clarification);
        await db.SaveChangesAsync(ct);
        return await ResponseAsync(booking, ct);
    }

    /// <summary>The agreement as it would be generated now, with this clarification. Nothing is saved.</summary>
    [HttpPost("preview"), ManagementOnly]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Preview(Guid id, ClarificationRequest request, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null) return NotFound();

        var number = await db.AgreementVersions.CountAsync(v => v.BookingId == id, ct) + 1;
        var pdf = renderer.Render(await ContentAsync(booking, number, request.Clarification, preview: true, ct));
        return File(pdf, "application/pdf", "aftale-forhaandsvisning.pdf");
    }

    /// <summary>Generates and keeps a new version. Earlier versions stay as they were.</summary>
    [HttpPost("versions"), ManagementOnly]
    public async Task<ActionResult<AgreementResponse>> Generate(Guid id, ClarificationRequest request, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        if (booking is null) return NotFound();
        booking.SetAgreementClarification(request.Clarification);

        var number = await db.AgreementVersions.CountAsync(v => v.BookingId == id, ct) + 1;
        var now = clock.GetUtcNow();
        var pdf = renderer.Render(await ContentAsync(booking, number, booking.AgreementClarification, preview: false, ct) with { GeneratedAt = now });
        var fileId = await files.SaveAsync(FileName(booking, number), "application/pdf", pdf, ct);

        db.AgreementVersions.Add(AgreementVersion.Create(booking.Id, number, currentUser.User.Name, booking.AgreementClarification, fileId, now));
        await db.SaveChangesAsync(ct);
        return await ResponseAsync(booking, ct);
    }

    [HttpGet("versions/{versionId:guid}/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Pdf(Guid id, Guid versionId, CancellationToken ct)
    {
        var version = await db.AgreementVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.BookingId == id, ct);
        return version is null ? NotFound() : await FileAsync(version.PdfFileId, ct);
    }

    [HttpGet("versions/{versionId:guid}/signed")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> Signed(Guid id, Guid versionId, CancellationToken ct)
    {
        var version = await db.AgreementVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.BookingId == id, ct);
        return version?.SignedFileId is { } fileId ? await FileAsync(fileId, ct) : NotFound();
    }

    /// <summary>The signed PDF that came back by mail. Marks the booking signed.</summary>
    [HttpPost("versions/{versionId:guid}/signed"), ManagementOnly]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<AgreementResponse>> UploadSigned(Guid id, Guid versionId, IFormFile file, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        var version = await db.AgreementVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.BookingId == id, ct);
        if (booking is null || version is null) return NotFound();

        var content = await ReadPdfAsync(file, ct);
        var fileId = await files.SaveAsync($"underskrevet-{FileName(booking, version.Number)}", "application/pdf", content, ct);

        var now = clock.GetUtcNow();
        version.RecordSigned(fileId, currentUser.User.Name, now);
        booking.MarkSigned(now);
        await db.SaveChangesAsync(ct);
        return await ResponseAsync(booking, ct);
    }

    /// <summary>
    /// Attaches the version to the latest quote in the accounting system, as Johan
    /// does in e-conomic today. Refused once the quote is no longer a draft.
    /// </summary>
    [HttpPost("versions/{versionId:guid}/attach-to-quote"), ManagementOnly]
    public async Task<IActionResult> AttachToQuote(Guid id, Guid versionId, CancellationToken ct)
    {
        var booking = await LoadAsync(id, ct);
        var version = await db.AgreementVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.BookingId == id, ct);
        if (booking is null || version is null) return NotFound();
        var quote = booking.LatestQuote ?? throw new DomainException("Opret et tilbud i regnskabet først.");

        var stored = await files.GetAsync(version.PdfFileId, ct) ?? throw new DomainException("Aftalens PDF findes ikke.");
        await accounting.AttachPdfAsync(quote.Number, stored.FileName, stored.Content, ct);
        return NoContent();
    }

    private Task<Booking?> LoadAsync(Guid id, CancellationToken ct) => db.WithCustomer().FirstOrDefaultAsync(b => b.Id == id, ct);

    private async Task<AgreementResponse> ResponseAsync(Booking booking, CancellationToken ct)
    {
        var versions = await db.AgreementVersions.AsNoTracking()
            .Where(v => v.BookingId == booking.Id)
            .OrderByDescending(v => v.Number)
            .Select(v => new AgreementVersionResponse(v.Id, v.Number, v.GeneratedAt, v.GeneratedBy, v.Clarification, v.SignedAt, v.SignedUploadedBy))
            .ToListAsync(ct);
        return new AgreementResponse(booking.AgreementClarification, versions);
    }

    private async Task<AgreementContent> ContentAsync(Booking booking, int number, string? clarification, bool preview, CancellationToken ct)
    {
        var price = booking.Price();
        var payments = await finance.ForAsync(booking, ct);
        var customer = booking.Customer;

        var lines = booking.OrderedLines.Zip(price.Lines, (line, priced) => new AgreementLine(
            line.Description,
            GroupName(line.Group),
            line.Pricing == LinePricing.PerGuest ? $"{priced.Units} gæster" : $"{priced.Units} stk.",
            priced.UnitPrice,
            priced.Total)).ToList();

        return new AgreementContent(
            number,
            clock.GetUtcNow(),
            customer.Name,
            customer.Address,
            customer.PostalCode,
            customer.City,
            customer.Contacts.OrderByDescending(c => c.IsPrimary).Select(c => new AgreementContact(c.Name, c.Email, c.Phone)).ToList(),
            booking.EventDate,
            booking.GuestCount,
            lines,
            price.Surcharge > 0 ? price.Surcharge : null,
            booking.Surcharge.Threshold,
            price.Total,
            booking.PaymentTerms == PaymentTerms.New
                ? "Nye betingelser: reservationsgebyr ved booking, 33 % af prisen seks måneder før og resten 30 dage før brylluppet."
                : "Reservationsgebyr ved booking og resten 30 dage før brylluppet.",
            payments.Instalments.Select(i => new AgreementPayment(InstalmentName(i.Kind), i.Amount, i.DueDate)).ToList(),
            Text(clarification),
            preview);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IActionResult> FileAsync(Guid fileId, CancellationToken ct)
    {
        var stored = await files.GetAsync(fileId, ct);
        return stored is null ? NotFound() : File(stored.Content, stored.ContentType, stored.FileName);
    }

    private static async Task<byte[]> ReadPdfAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new DomainException("Vælg den underskrevne PDF.");
        if (file.Length > MaxUploadBytes) throw new DomainException("Filen er for stor (højst 15 MB).");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        // The file's own first bytes, not its name or the browser's word for it.
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) throw new DomainException("Filen er ikke en PDF.");
        return bytes;
    }

    private static string FileName(Booking booking, int number) => $"aftale-{booking.EventDate:yyyy-MM-dd}-v{number}.pdf";

    private static string InstalmentName(InstalmentKind kind) => kind switch
    {
        InstalmentKind.ReservationFee => "Reservationsgebyr",
        InstalmentKind.Instalment => "33 %-rate",
        _ => "Restbetaling",
    };

    private static string GroupName(ProductGroup group) => group switch
    {
        ProductGroup.Reception => "Reception",
        ProductGroup.Ceremony => "Vielse",
        ProductGroup.DinnerAndParty => "Middag og fest",
        ProductGroup.LateNight => "Natmad",
        ProductGroup.Bar => "Bar",
        ProductGroup.Breakfast => "Morgenmad",
        ProductGroup.FridayEvent => "Fredagsarrangement",
        _ => "Andet",
    };
}
