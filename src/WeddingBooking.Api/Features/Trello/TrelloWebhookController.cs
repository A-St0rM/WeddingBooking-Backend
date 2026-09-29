using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WeddingBooking.Api.Auth;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Trello;
using WeddingBooking.Integrations.Trello;

namespace WeddingBooking.Api.Features.Trello;

public sealed record WebhooksResponse(string CallbackUrl, IReadOnlyList<string> Registered,
    // Board and Trello's answer. Trello refuses a webhook that already exists, which is fine.
    IReadOnlyList<string> Refused);

/// <summary>
/// Trello calls here when a board changes. Anyone can reach the address, so a call only counts when
/// Trello's signature over the body and our callback address checks out with the app secret. The
/// call is only a nudge: the board is then read from Trello, never taken from the body.
/// </summary>
[ApiController]
[Route("api/trello/webhook")]
[AllowAnonymous]
public sealed class TrelloWebhookController(TrelloSync sync, IOptions<TrelloOptions> options, ILogger<TrelloWebhookController> logger) : ControllerBase
{
    /// <summary>Trello checks the address with a HEAD before it registers the webhook.</summary>
    [HttpHead]
    public IActionResult Probe() => Ok();

    [HttpPost, RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        if (!IsSigned(body, Request.Headers["X-Trello-Webhook"].ToString(), options.Value)) return Unauthorized();

        var boardId = JsonNode.Parse(body)?["model"]?["id"]?.ToString();
        if (boardId is null || !sync.BoardIds.Contains(boardId)) return Ok();
        try
        {
            await sync.SyncBoardAsync(boardId, ct);
        }
        catch (Exception error) when (error is DomainException or HttpRequestException)
        {
            // Trello retries on errors and gives up on a webhook that keeps failing; the reconciliation catches up instead.
            logger.LogWarning(error, "Webhook sync of board {BoardId} failed.", boardId);
        }
        return Ok();
    }

    public static bool IsSigned(string body, string signature, TrelloOptions trello)
    {
        if (string.IsNullOrEmpty(trello.AppSecret) || string.IsNullOrEmpty(trello.CallbackUrl) || signature.Length == 0) return false;
        var expected = HMACSHA1.HashData(Encoding.UTF8.GetBytes(trello.AppSecret), Encoding.UTF8.GetBytes(body + trello.CallbackUrl));
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Asks Trello to call the webhook for every configured board. Safe to run again.</summary>
[ApiController]
[Route("api/trello/webhooks")]
[ManagementOnly]
public sealed class TrelloWebhookSetupController(ITrelloGateway trello, IOptions<TrelloOptions> options) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WebhooksResponse>> Register(CancellationToken ct)
    {
        var callback = options.Value.CallbackUrl ?? throw new DomainException("Trello:CallbackUrl er ikke sat.");
        var registered = new List<string>();
        var refused = new List<string>();
        foreach (var boardId in options.Value.BoardIds)
        {
            try
            {
                await trello.RegisterWebhookAsync(boardId, callback, ct);
                registered.Add(boardId);
            }
            catch (DomainException error)
            {
                refused.Add($"{boardId}: {error.Message}");
            }
        }
        return new WebhooksResponse(callback, registered, refused);
    }
}
