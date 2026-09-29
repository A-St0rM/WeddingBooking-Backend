namespace WeddingBooking.Domain.Messages;

/// <summary>
/// Sends a mail. Called only after a person has read the text and pressed Send.
/// Today a simulated outbox; a real adapter can send through one.com's SMTP.
/// </summary>
public interface IMessageSender
{
    bool IsSimulated { get; }

    Task<string> SendAsync(OutgoingMessage message, CancellationToken ct);
}

public sealed record OutgoingMessage(
    string FromName,
    string? ReplyTo,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body);
