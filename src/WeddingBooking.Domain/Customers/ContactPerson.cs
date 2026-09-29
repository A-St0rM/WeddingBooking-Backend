using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Customers;

/// <summary>One of the people behind a customer — normally the two getting married.</summary>
public sealed class ContactPerson
{
    private ContactPerson() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>The one mail goes to. Exactly one per customer; the customer keeps it so.</summary>
    public bool IsPrimary { get; internal set; }

    internal static ContactPerson Create(Guid customerId, string name, string? email, string? phone) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customerId,
        Name = Text.Required(name, "En kontaktperson skal have et navn."),
        Email = Text.Blank(email)?.ToLowerInvariant(),
        Phone = Text.Blank(phone),
    };

    internal void Update(string name, string? email, string? phone)
    {
        Name = Text.Required(name, "En kontaktperson skal have et navn.");
        Email = Text.Blank(email)?.ToLowerInvariant();
        Phone = Text.Blank(phone);
    }
}
