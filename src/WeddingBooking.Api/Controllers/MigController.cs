using Microsoft.AspNetCore.Mvc;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Contracts;

namespace WeddingBooking.Api.Controllers;

[ApiController]
[Route("api/mig")]
public sealed class MigController(IBrugerAccessor brugere) : ControllerBase
{
    /// <summary>Who the caller is, as this system understands them.</summary>
    [HttpGet]
    public async Task<ActionResult<MigResponse>> Hent(CancellationToken cancellationToken)
    {
        var bruger = await brugere.HentAsync(cancellationToken);

        // Unreachable in practice: the middleware refuses unknown callers first.
        if (bruger is null)
        {
            return Forbid();
        }

        return Ok(new MigResponse(bruger.Id, bruger.Navn, bruger.Email, bruger.Rolle.ToString()));
    }
}
