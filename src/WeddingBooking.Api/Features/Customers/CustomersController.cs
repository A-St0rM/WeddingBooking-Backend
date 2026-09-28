using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Api.Features.Bookings;
using WeddingBooking.Domain.Accounting;
using WeddingBooking.Domain.Bookings;
using WeddingBooking.Domain.Common;
using WeddingBooking.Domain.Customers;
using WeddingBooking.Infrastructure.Persistence;

namespace WeddingBooking.Api.Features.Customers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db, BookingResponder responder, IAccountingSystem accounting) : ControllerBase
{
    /// <summary>Customers by name, contact name or e-mail — fast enough to call on every keystroke.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<CustomerSummaryResponse>> List([FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            foreach (var pattern in Search.CouplePatterns(search))
            {
                query = query.Where(customer => customer.Contacts.Any(contact =>
                    EF.Functions.ILike(contact.FirstName, pattern)
                    || EF.Functions.ILike(contact.LastName!, pattern)
                    || EF.Functions.ILike(contact.Email!, pattern)));
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await query
            .OrderBy(customer => customer.Contacts.Where(contact => contact.Position == 1).Select(contact => contact.FirstName).FirstOrDefault())
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
                row.Customer.Payer is { } payer ? ContactResponse.From(payer) : null,
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

    /// <summary>Customers left over from before the couple was two people, and why each is not. The migration's report.</summary>
    [HttpGet("incomplete"), ManagementOnly]
    public async Task<IReadOnlyList<IncompleteCustomerResponse>> Incomplete(CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking().Where(c => c.Contacts.Count != 2 || c.Contacts.Count(p => p.IsPayer) != 1).ToListAsync(ct);
        return customers.Select(c => new IncompleteCustomerResponse(c.Id, c.Name, c.Contacts.Count,
                c.Contacts.Count < 2 ? "Navnet kunne ikke deles i to personer. Tilføj den anden person."
                : c.Contacts.Count > 2 ? "Kunden har flere end to personer. Fjern dem, der ikke er parret."
                : "Kunden har ingen betaler."))
            .ToList();
    }

    [HttpPut("{id:guid}"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.UpdateAddress(request.Address, request.PostalCode, request.City), ct);

    [HttpPost("{id:guid}/contacts"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> AddMissingPartner(Guid id, PartnerRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.AddMissingPartner(request.ToDetails()), ct);

    [HttpPut("{id:guid}/contacts/{contactId:guid}"), ManagementOnly]
    public Task<ActionResult<CustomerDetailResponse>> UpdatePartner(Guid id, Guid contactId, PartnerRequest request, CancellationToken ct) =>
        ChangeAsync(id, customer => customer.UpdatePartner(contactId, request.ToDetails()), ct);

    /// <summary>Chooses who pays. Allowed until the first invoice is booked — after that the invoice carries a name.</summary>
    [HttpPost("{id:guid}/contacts/{contactId:guid}/payer"), ManagementOnly]
    public async Task<ActionResult<CustomerDetailResponse>> MakePayer(Guid id, Guid contactId, CancellationToken ct)
    {
        var invoices = await db.Bookings.Where(b => b.CustomerId == id)
            .SelectMany(b => b.AccountingReferences)
            .Where(r => r.Type == DocumentType.Invoice)
            .Select(r => r.Number)
            .ToListAsync(ct);
        if (invoices.Count > 0 && (await accounting.GetDocumentsAsync(invoices, ct)).Any(d => d.Status != DocumentStatus.Draft))
        {
            throw new DomainException("Der er bogført en faktura i betalerens navn. Betaleren kan ikke skiftes derefter.");
        }

        return await ChangeAsync(id, customer => customer.MakePayer(contactId), ct);
    }

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
        return new CustomerDetailResponse(CustomerResponse.From(customer), await responder.SummariesAsync(bookings, ct));
    }
}
