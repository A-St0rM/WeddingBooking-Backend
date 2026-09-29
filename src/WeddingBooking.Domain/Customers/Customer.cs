using System.Text.RegularExpressions;
using WeddingBooking.Domain.Common;

namespace WeddingBooking.Domain.Customers;

/// <summary>
/// The couple: two people, one of them the payer. Outlives its bookings — a
/// couple who come back are the same customer, not a new one.
/// </summary>
public sealed partial class Customer
{
    private readonly List<ContactPerson> _contacts = [];

    private Customer() { }

    public Guid Id { get; private set; }

    /// <summary>The payer's address — the one on the invoice.</summary>
    public string? Address { get; private set; }

    public string? PostalCode { get; private set; }
    public string? City { get; private set; }

    /// <summary>The customer number in the accounting system, once it has one. A reference, nothing more.</summary>
    public string? AccountingNumber { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ContactPerson> Contacts => _contacts;

    /// <summary>The two, in the order they are named.</summary>
    public IEnumerable<ContactPerson> Partners => _contacts.OrderBy(contact => contact.Position);

    /// <summary>"Tobias og Cecilie" — worked out from the two first names, never stored.</summary>
    public string Name => string.Join(" og ", Partners.Select(partner => partner.FirstName));

    public ContactPerson? Payer => _contacts.FirstOrDefault(contact => contact.IsPayer);

    /// <summary>A new couple. The first named is the payer until someone chooses otherwise.</summary>
    public static Customer Create(PartnerDetails first, PartnerDetails second)
    {
        var customer = new Customer { Id = Guid.CreateVersion7(), CreatedAt = DateTimeOffset.UtcNow };
        customer._contacts.Add(ContactPerson.Create(customer.Id, 1, first));
        customer._contacts.Add(ContactPerson.Create(customer.Id, 2, second));
        customer._contacts[0].IsPayer = true;
        return customer;
    }

    public void UpdateAddress(string? address, string? postalCode, string? city)
    {
        Address = Text.Blank(address);
        PostalCode = Text.Blank(postalCode);
        City = Text.Blank(city);
    }

    public void RecordAccountingNumber(string number) => AccountingNumber = number;

    /// <summary>For a customer from before the couple was two people: adds the one missing.</summary>
    public ContactPerson AddMissingPartner(PartnerDetails details)
    {
        if (_contacts.Count >= 2) throw new DomainException("Kunden har allerede to personer.");
        var person = ContactPerson.Create(Id, _contacts.Count == 0 ? 1 : _contacts.Max(c => c.Position) + 1, details);
        person.IsPayer = _contacts.Count == 0;
        _contacts.Add(person);
        return person;
    }

    public void UpdatePartner(Guid contactId, PartnerDetails details) => Find(contactId).Update(details);

    /// <summary>Makes this person the payer. Whether that is still allowed — no invoice booked yet — is decided by the caller, who can ask the accounting system.</summary>
    public void MakePayer(Guid contactId)
    {
        var chosen = Find(contactId);
        foreach (var contact in _contacts)
        {
            contact.IsPayer = contact == chosen;
        }
    }

    /// <summary>"Tobias & Cecilie" → ("Tobias", "Cecilie"). Null when the name is not two people joined by og, & or +.</summary>
    public static (string First, string Second)? SplitCouple(string? name)
    {
        var parts = CoupleSeparator().Split(name?.Trim() ?? "");
        return parts is [var first, var second] && first.Trim().Length > 0 && second.Trim().Length > 0
            ? (first.Trim(), second.Trim())
            : null;
    }

    private ContactPerson Find(Guid contactId) =>
        _contacts.FirstOrDefault(contact => contact.Id == contactId)
        ?? throw new DomainException("Personen findes ikke på kunden.");

    [GeneratedRegex(@"\s+(?:og|&|\+)\s+|\s*[&+]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex CoupleSeparator();
}
