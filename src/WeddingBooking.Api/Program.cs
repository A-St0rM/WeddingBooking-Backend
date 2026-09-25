var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// App Service's health check is configured to probe this path. Without it the
// app is marked unhealthy and restarted in a loop.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
