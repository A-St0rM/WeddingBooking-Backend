using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Customers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db) : ControllerBase
{
    /// <summary>Customers by name, contact name or e-mail — fast enough to call on every keystroke.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<CustomerSummaryResponse>> List([FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Search.Contains(search);
            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, pattern)
                || customer.Contacts.Any(contact =>
                    EF.Functions.ILike(contact.Name, pattern) || EF.Functions.ILike(contact.Email!, pattern)));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await query
            .OrderBy(customer => customer.Name)
            .Take(string.IsNullOrWhiteSpace(search) ? 500 : 20)
            .Select(customer => new
            {
                Customer = customer,
                BookingCount = db.Bookings.Count(booking => booking.CustomerId == customer.Id),
                NextEventDate = db.Bookings
                    .Where(booking => booking.CustomerId == customer.Id && booking.EventDate >= today
                        && booking.Stage != SalesStage.Cancelled && booking.Stage != SalesStage.Lost)
                    .Min(booking => (DateOnly?)booking.EventDate),
            })
            .ToListAsync(ct);

        return rows.Select(row => new CustomerSummaryResponse(
                row.Customer.Id,
                row.Customer.Name,
                row.Customer.City,
                row.Customer.PrimaryContact is { } primary ? ContactResponse.From(primary) : null,
                row.BookingCount,
                row.NextEventDate))
            .ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailResponse>> Get(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();
        return await DetailAsync(customer, ct);
    }

    [HttpPost, ManagementOnly]
    public async Task<ActionResult<CustomerDetailResponse>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = Customer.Create(request.Name);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, await DetailAsync(customer, ct));
    }

    [HttpPut("{id:guid}"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.UpdateDetails(request.Name, request.Address, request.PostalCode, request.City), ct);

    [HttpPost("{id:guid}/contacts"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> AddContact(Guid id, ContactRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.AddContact(request.Name, request.Email, request.Phone), ct);

    [HttpPut("{id:guid}/contacts/{contactId:guid}"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> UpdateContact(Guid id, Guid contactId, ContactRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.UpdateContact(contactId, request.Name, request.Email, request.Phone), ct);

    [HttpPost("{id:guid}/contacts/{contactId:guid}/primary"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> MakePrimary(Guid id, Guid contactId, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.MakePrimary(contactId), ct);

    [HttpDelete("{id:guid}/contacts/{contactId:guid}"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> RemoveContact(Guid id, Guid contactId, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.RemoveContact(contactId), ct);

    private async Task<ActionResult<CustomerDetailResponse>> ChangeAsync(Guid id, Action<Customer> change, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();
        change(customer);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(customer, ct);
    }

    private async Task<CustomerDetailResponse> DetailAsync(Customer customer, CancellationToken ct)
    {
        var bookings = await db.WithCustomer().AsNoTracking()
            .Where(booking => booking.CustomerId == customer.Id)
            .OrderByDescending(booking => booking.EventDate)
            .ToListAsync(ct);
        return new CustomerDetailResponse(CustomerResponse.From(customer), bookings.Select(BookingSummaryResponse.From).ToList());
    }
}
