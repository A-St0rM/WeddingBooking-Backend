using System.ComponentModel.DataAnnotations;
using WeddingBooking.Domain.Customers;

namespace WeddingBooking.Api.Features.Customers;

public sealed record ContactResponse(Guid Id, string Name, string? Email, string? Phone, bool IsPrimary)
{
    public static ContactResponse From(ContactPerson contact) =>
        new(contact.Id, contact.Name, contact.Email, contact.Phone, contact.IsPrimary);
}

public sealed record CustomerSummaryResponse(
    Guid Id,
    string Name,
    string? City,
    ContactResponse? PrimaryContact,
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
        customer.Contacts.OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name)
            .Select(ContactResponse.From).ToList());
}

public sealed record CustomerDetailResponse(CustomerResponse Customer, IReadOnlyList<Bookings.BookingSummaryResponse> Bookings);

public sealed record CreateCustomerRequest([Required, MaxLength(200)] string Name);

public sealed record UpdateCustomerRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200)] string? Address,
    [MaxLength(10)] string? PostalCode,
    [MaxLength(100)] string? City);

public sealed record ContactRequest(
    [Required, MaxLength(200)] string Name,
    [EmailAddress, MaxLength(320)] string? Email,
    [MaxLength(40)] string? Phone);
