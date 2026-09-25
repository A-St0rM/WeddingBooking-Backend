using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Messages;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Messages;

public sealed record MessageTemplateResponse(Guid Id, string Name, string Subject, string Body, bool CcViewingHost)
{
    public static MessageTemplateResponse From(MessageTemplate t) => new(t.Id, t.Name, t.Subject, t.Body, t.CcViewingHost);
}

public sealed record PlaceholderResponse(string Name, string Description);

public sealed record MessageTemplateRequest(
    [Required, MaxLength(200)] string Name, [Required, MaxLength(500)] string Subject, [Required, MaxLength(20_000)] string Body, bool CcViewingHost);

/// <summary>Johan's library of standard texts.</summary>
[ApiController]
[Route("api/message-templates")]
[ManagementOnly]
public sealed class MessageTemplatesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<MessageTemplateResponse>> List(CancellationToken ct) =>
        (await db.MessageTemplates.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct))
            .Select(MessageTemplateResponse.From).ToList();

    [HttpGet("placeholders")]
    public IReadOnlyList<PlaceholderResponse> Placeholders() =>
        TemplateMerger.Placeholders.Select(p => new PlaceholderResponse(p.Key, p.Value)).ToList();

    [HttpPost]
    public async Task<MessageTemplateResponse> Create(MessageTemplateRequest request, CancellationToken ct)
    {
        var order = await db.MessageTemplates.CountAsync(ct);
        var template = MessageTemplate.Create(request.Name, request.Subject, request.Body, request.CcViewingHost, order);
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return MessageTemplateResponse.From(template);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MessageTemplateResponse>> Update(Guid id, MessageTemplateRequest request, CancellationToken ct)
    {
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        template.Update(request.Name, request.Subject, request.Body, request.CcViewingHost);
        await db.SaveChangesAsync(ct);
        return MessageTemplateResponse.From(template);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        db.MessageTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
