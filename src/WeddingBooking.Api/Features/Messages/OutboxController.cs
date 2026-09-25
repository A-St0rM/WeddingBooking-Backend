using Microsoft.AspNetCore.Mvc;
using WeddingBooking.Api.Auth;
using WeddingBooking.Integrations.Mail.Simulated;

namespace WeddingBooking.Api.Features.Messages;

public sealed record OutboxMessageResponse(Guid Id, string FromName, string? ReplyTo, string To, string Cc, string Subject, string Body, DateTimeOffset SentAt);

/// <summary>The simulated outbox: every mail "sent", as it would have gone out.</summary>
[ApiController]
[Route("api/outbox")]
[ManagementOnly]
public sealed class OutboxController(SimulatedOutbox outbox) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<OutboxMessageResponse>> List(CancellationToken ct) =>
        (await outbox.ListAsync(ct)).Select(m => new OutboxMessageResponse(m.Id, m.FromName, m.ReplyTo, m.To, m.Cc, m.Subject, m.Body, m.SentAt)).ToList();
}
