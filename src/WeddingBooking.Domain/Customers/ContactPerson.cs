using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Customers;

/// <summary>What is typed for one of the two people getting married.</summary>
public sealed record PartnerDetails(string FirstName, string? LastName = null, string? Email = null, string? Phone = null);

/// <summary>One of the two people behind a customer. Never written together with the other in one field.</summary>
public sealed class ContactPerson
{
    private ContactPerson() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }

    /// <summary>1 or 2: the order the couple is named in. Keeps "Tobias og Cecilie" from turning round.</summary>
    public int Position { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string? LastName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>The one on the quote and the invoice, and the one created in the accounting system. Exactly one per customer.</summary>
    public bool IsPayer { get; internal set; }

    public string FullName => LastName is null ? FirstName : $"{FirstName} {LastName}";

    internal static ContactPerson Create(Guid customerId, int position, PartnerDetails details)
    {
        var person = new ContactPerson { Id = Guid.CreateVersion7(), CustomerId = customerId, Position = position };
        person.Update(details);
        return person;
    }

    internal void Update(PartnerDetails details)
    {
        FirstName = Text.Required(details.FirstName, "Begge personer skal have et fornavn.");
        LastName = Text.Blank(details.LastName);
        Email = Text.Blank(details.Email)?.ToLowerInvariant();
        Phone = Text.Blank(details.Phone);
    }
}
