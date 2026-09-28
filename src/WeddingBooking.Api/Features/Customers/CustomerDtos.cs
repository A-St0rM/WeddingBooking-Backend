using System.ComponentModel.DataAnnotations;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Api.Features.Customers;

public sealed record ContactResponse(Guid Id, int Position, string FirstName, string? LastName, string FullName, string? Email, string? Phone, bool IsPayer)
{
    public static ContactResponse From(ContactPerson contact) =>
        new(contact.Id, contact.Position, contact.FirstName, contact.LastName, contact.FullName, contact.Email, contact.Phone, contact.IsPayer);
}

public sealed record CustomerSummaryResponse(
    Guid Id,
    string Name,
    string? City,
    ContactResponse? Payer,
    int BookingCount,
    DateOnly? NextEventDate);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Address,
    string? PostalCode,
    string? City,
    IReadOnlyList<ContactResponse> Contacts)
{
    public static CustomerResponse From(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Address,
        customer.PostalCode,
        customer.City,
        customer.Partners.Select(ContactResponse.From).ToList());
}

public sealed record CustomerDetailResponse(CustomerResponse Customer, IReadOnlyList<Bookings.BookingSummaryResponse> Bookings);

// The payer's address — the one on the invoice.
public sealed record UpdateCustomerRequest(
    [MaxLength(200)] string? Address,
    [MaxLength(10)] string? PostalCode,
    [MaxLength(100)] string? City);

public sealed record PartnerRequest(
    [Required, MaxLength(100)] string FirstName,
    [MaxLength(100)] string? LastName,
    [EmailAddress, MaxLength(320)] string? Email,
    [MaxLength(40)] string? Phone)
{
    public PartnerDetails ToDetails() => new(FirstName, LastName, Email, Phone);
}

// A customer that does not have exactly two people — left over from before the couple was two people.
public sealed record IncompleteCustomerResponse(Guid Id, string Name, int People, string Reason);
