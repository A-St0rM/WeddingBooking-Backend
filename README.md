# WeddingBooking API

## Run it

The schema is applied by migration, never by hand. Against a fresh database:

```bash
export ConnectionStrings__Postgres="Host=...;Database=...;Username=...;Password=..."
dotnet ef database update --project src/WeddingBooking.Infrastructure --startup-project src/WeddingBooking.Api
dotnet run --project src/WeddingBooking.Api
```

The application deliberately does **not** migrate on startup: applying schema changes is a decision someone makes, not a side effect of a deploy.

Health check at `/health`, OpenAPI at `/openapi/v1.json` in Development.

Every integration defaults to `Fake`, so it starts with no credentials at all.

## Run it faked, end to end

```bash
ASPNETCORE_ENVIRONMENT=Demo dotnet run --project src/WeddingBooking.Api
```

## Test

```bash
dotnet test
```

`WeddingBooking.Domain.Tests` runs anywhere.

`WeddingBooking.Api.Tests` boots the real application and needs a real Postgres. Point it at one and the whole suite runs:

```bash
export WEDDINGBOOKING_TEST_POSTGRES="Host=...;Database=...;Username=...;Password=..."
dotnet test
```

Without that variable the database-backed tests **skip** rather than pass, and say so by name. A green run with skips is not a green run.

## Secrets

None are committed. Set them locally with:

```bash
dotnet user-secrets set "Economic:AppSecretToken" "..." --project src/WeddingBooking.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "..." --project src/WeddingBooking.Api
```

See `CLAUDE.md` for the full layout and the rules that are easy to break by accident.
