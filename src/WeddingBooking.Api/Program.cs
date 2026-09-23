using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using WeddingBooking.Api.Auth;
using WeddingBooking.Api.Configuration;
using WeddingBooking.Api.Middleware;
using WeddingBooking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.SectionName))
    .Configure<EconomicOptions>(builder.Configuration.GetSection(EconomicOptions.SectionName))
    .Configure<TrelloOptions>(builder.Configuration.GetSection(TrelloOptions.SectionName))
    .Configure<SupabaseOptions>(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));

builder.Services.AddPersistence(builder.Configuration.GetConnectionString("Postgres"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IBrugerAccessor, BrugerAccessor>();

// Login is Supabase's. Every authorisation decision after it is ours. ADR-0002.
var supabase = builder.Configuration
    .GetSection(SupabaseOptions.SectionName)
    .Get<SupabaseOptions>() ?? new SupabaseOptions();

// Supabase issues tokens from /auth/v1, not from the project root, and signs
// them with rotating ES256 keys published at that authority's JWKS endpoint.
// Validating against those public keys means this API holds no shared secret.
var authority = string.IsNullOrWhiteSpace(supabase.Url)
    ? null
    : $"{supabase.Url.TrimEnd('/')}/auth/v1";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (authority is not null)
        {
            options.Authority = authority;
        }

        options.Audience = supabase.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = authority is not null,
            ValidIssuer = authority,
            ValidateAudience = !string.IsNullOrWhiteSpace(supabase.Audience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Every endpoint needs a signed-in caller unless it says otherwise. Opting in
// per controller is how one forgotten attribute becomes an open endpoint.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

const string FrontendCorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// App Service terminates TLS and forwards the request over plain HTTP. Without
// this the app believes every request is insecure, and UseHttpsRedirection
// answers with a redirect to a URL the client already asked for — a loop.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<KendtBrugerMiddleware>();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

/// Exposed so the integration tests can boot the real application
/// through WebApplicationFactory. This is the primary test seam.
public partial class Program;
