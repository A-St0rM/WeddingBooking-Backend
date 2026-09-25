using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Common;
using WeddingBooking.Domain.Users;
using WeddingBooking.Integrations.Accounting.Simulated;

namespace WeddingBooking.Api.Features.AccountingSim;

public sealed record RegisterPaymentRequest([Range(0.01, 10_000_000)] decimal Amount, DateOnly? PaidOn);

/// <summary>
/// The simulated accounting system's own screens: what the bookkeeper would do
/// in e-conomic. Not part of the booking system — it exists so the flow from
/// quote to full payment can be shown to the end.
/// </summary>
[ApiController]
[Route("api/accounting-sim")]
[PriceRolesOnly]
public sealed class AccountingSimController(SimulatedAccountingAdmin admin, CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    [HttpGet("documents")]
    public Task<IReadOnlyList<SimDocumentSummary>> Documents(CancellationToken ct) => admin.ListAsync(ct);

    [HttpPost("documents/{number}/sent"), ManagementOnly]
    public async Task<IActionResult> MarkSent(string number, CancellationToken ct)
    {
        await admin.MarkQuoteSentAsync(number, ct);
        return NoContent();
    }

    [HttpPost("documents/{number}/payments"), AllowRoles(Role.EventManager, Role.Owner, Role.Bookkeeper)]
    public async Task<IActionResult> RegisterPayment(string number, RegisterPaymentRequest request, CancellationToken ct)
    {
        await admin.RegisterPaymentAsync(number, request.Amount, request.PaidOn ?? Today.In(clock), currentUser.User.Name, ct);
        return NoContent();
    }
}
