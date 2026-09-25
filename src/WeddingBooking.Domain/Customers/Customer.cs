using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Customers;

/// <summary>
/// The couple, as one party. Outlives its bookings: a couple who come back are
/// the same customer, not a new one.
/// </summary>
public sealed class Customer
{
    private readonly List<ContactPerson> _contacts = [];

    private Customer() { }

    public Guid Id { get; private set; }

    /// <summary>How the couple is referred to — "Tobias og Cecilie".</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Address { get; private set; }
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ContactPerson> Contacts => _contacts;

    public ContactPerson? PrimaryContact => _contacts.FirstOrDefault(contact => contact.IsPrimary);

    public static Customer Create(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = Text.Required(name, "Kunden skal have et navn."),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public void UpdateDetails(string name, string? address, string? postalCode, string? city)
    {
        Name = Text.Required(name, "Kunden skal have et navn.");
        Address = Text.Blank(address);
        PostalCode = Text.Blank(postalCode);
        City = Text.Blank(city);
    }

    /// <summary>Adds a contact. The first one added becomes the primary.</summary>
    public ContactPerson AddContact(string name, string? email, string? phone)
    {
        var contact = ContactPerson.Create(Id, name, email, phone);
        contact.IsPrimary = _contacts.Count == 0;
        _contacts.Add(contact);
        return contact;
    }

    public void UpdateContact(Guid contactId, string name, string? email, string? phone) =>
        Find(contactId).Update(name, email, phone);

    public void MakePrimary(Guid contactId)
    {
        var chosen = Find(contactId);
        foreach (var contact in _contacts)
        {
            contact.IsPrimary = contact == chosen;
        }
    }

    /// <summary>Removes a contact; if it was the primary, the next one takes over.</summary>
    public void RemoveContact(Guid contactId)
    {
        var contact = Find(contactId);
        _contacts.Remove(contact);
        if (contact.IsPrimary && _contacts.Count > 0)
        {
            _contacts[0].IsPrimary = true;
        }
    }

    private ContactPerson Find(Guid contactId) =>
        _contacts.FirstOrDefault(contact => contact.Id == contactId)
        ?? throw new DomainException("Kontaktpersonen findes ikke på kunden.");
}
