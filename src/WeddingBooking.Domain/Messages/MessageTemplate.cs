using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Messages;

/// <summary>One of Johan's standard texts, with placeholders the booking fills in.</summary>
public sealed class MessageTemplate
{
    private MessageTemplate() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>Puts the viewing's host on CC — Bryan on a meeting confirmation.</summary>
    public bool CcViewingHost { get; private set; }

    public int SortOrder { get; private set; }

    public static MessageTemplate Create(string name, string subject, string body, bool ccViewingHost, int sortOrder)
    {
        var template = new MessageTemplate { Id = Guid.CreateVersion7(), SortOrder = sortOrder };
        template.Update(name, subject, body, ccViewingHost);
        return template;
    }

    public void Update(string name, string subject, string body, bool ccViewingHost)
    {
        Name = Text.Required(name, "En tekstbid skal have et navn.");
        Subject = Text.Required(subject, "En tekstbid skal have et emne.");
        Body = Text.Required(body, "En tekstbid skal have en tekst.");
        CcViewingHost = ccViewingHost;
    }
}
