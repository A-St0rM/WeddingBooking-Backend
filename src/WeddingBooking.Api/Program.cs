using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using WeddingBooking.Api.Configuration;
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

// Login is Supabase's; every authorisation decision after it is ours. ADR-0002.
var supabase = builder.Configuration
    .GetSection(SupabaseOptions.SectionName)
    .Get<SupabaseOptions>() ?? new SupabaseOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(supabase.Url))
        {
            options.Authority = supabase.Url;
        }

        options.Audience = supabase.Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(supabase.Url),
            ValidateAudience = !string.IsNullOrWhiteSpace(supabase.Audience),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

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
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

/// Exposed so the integration tests can boot the real application
/// through WebApplicationFactory. This is the primary test seam.
public partial class Program;
