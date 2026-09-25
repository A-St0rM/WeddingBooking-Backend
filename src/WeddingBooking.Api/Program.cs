using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Configuration;
using WeddingBooking.Api.Middleware;
using WeddingBooking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var supabase = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();
builder.Services.Configure<AccessOptions>(builder.Configuration.GetSection(AccessOptions.SectionName));

builder.Services.AddPersistence(builder.Configuration.GetConnectionString("Postgres"));
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<WeddingBooking.Api.Features.Bookings.BookingResponder>();

builder.Services
    // One content type in the contract. Without this every response is described
    // three times (text/plain, text/json, application/json) and the generated
    // client carries all three.
    .AddControllers(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.ProducesAttribute("application/json")))
    .AddJsonOptions(options => Json.Configure(options.JsonSerializerOptions));
// Enums travel as their names and numbers as numbers. MVC serialises with the
// options above, but the OpenAPI document is generated from the minimal-API
// options — set both, or the contract says "number" while the API sends
// "EventManager", and "number | string" for every count and amount.
builder.Services.ConfigureHttpJsonOptions(options => Json.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Login is Supabase's; every decision after it is ours. Tokens are signed with
// Supabase's rotating ES256 keys, fetched from the authority's JWKS endpoint, so
// this API holds no shared secret.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = supabase.Authority;
        options.Audience = supabase.Audience;
        // Keep JWT claim names ("sub", "email") as issued rather than remapped to
        // the long WS-* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = supabase.Authority is not null,
            ValidIssuer = supabase.Authority,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Closed by default: every endpoint needs a signed-in caller unless it opts out.
// Opting in per controller is how one forgotten attribute becomes an open endpoint.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

const string FrontendCorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

// App Service terminates TLS and forwards plain HTTP. Without this every request
// looks insecure and UseHttpsRedirection redirects to the URL already asked for.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// The order is load-bearing; see CLAUDE.md.
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<KnownUserMiddleware>();

app.MapControllers();

// App Service's health check probes this path; without it the app is marked
// unhealthy and restarted in a loop.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.Run();

/// <summary>Exposed so tests can boot the real application through WebApplicationFactory.</summary>
public partial class Program;

internal static class Json
{
    public static void Configure(System.Text.Json.JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.NumberHandling = JsonNumberHandling.Strict;
    }
}
