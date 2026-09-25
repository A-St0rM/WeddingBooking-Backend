using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeddingBooking.Domain.Common;

namespace WeddingBooking.Api.Middleware;

/// <summary>
/// Turns exceptions into ProblemDetails without discarding the CORS headers
/// already on the response.
/// </summary>
/// <remarks>
/// The framework's own exception handling clears the response before writing,
/// stripping Access-Control-Allow-Origin — and the browser then reports a CORS
/// failure instead of the real one. Registered after UseCors, this writes onto
/// the response CORS has already prepared.
///
/// <para>A <see cref="DomainException"/> is a business rule saying no, and
/// becomes a 400 carrying the rule's own message. A concurrency conflict — two
    /// people saving the same booking — is a 409. Anything else is logged with a
/// trace id and returned as a 500 without the exception text, which can carry
/// connection strings.</para>
/// </remarks>
public sealed class UnhandledExceptionMiddleware(RequestDelegate next, ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException refusal) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "Kan ikke gennemføres", refusal.Message);
        }
        catch (ForbiddenException refusal) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, StatusCodes.Status403Forbidden, "Ingen adgang", refusal.Message);
        }
        catch (DbUpdateConcurrencyException) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "Ændret af en anden",
                "En anden har gemt en ændring imens. Hent den nyeste udgave, og ret igen.");
        }
        catch (Exception exception) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path} (trace {TraceId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            await WriteAsync(context, StatusCodes.Status500InternalServerError, "Serverfejl",
                "Noget gik galt på serveren. Fejlen er logget.");
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            },
            options: null,
            contentType: "application/problem+json");
    }
}
