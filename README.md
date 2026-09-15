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

## Connecting to Supabase

Use the **session pooler**, not the direct host:

```
Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=...;SSL Mode=Require;Trust Server Certificate=true
```

Three things there are easy to get wrong, and each fails differently:

- **The direct host `db.<ref>.supabase.co` has no IPv4 address at all.** On a free project it is IPv6-only, and most home networks cannot route to it: `No route to host`. The pooler has IPv4.
- **The username carries the project ref** — `postgres.<ref>`, not `postgres`. The pooler routes on it; get it wrong and it answers `Tenant or user not found`.
- **The pooler's certificate is self-signed**, so validation has to be switched off explicitly or the connection fails on the certificate rather than on anything real.

Use port **5432** (session mode). Port 6543 is transaction mode, which reuses connections in a way EF Core migrations do not tolerate.

If `aws-1` is ever wrong for a project, the dashboard's **Connect** dialog has the authoritative string.

## Secrets

None are committed. Set them locally with:

```bash
dotnet user-secrets set "Economic:AppSecretToken" "..." --project src/WeddingBooking.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "..." --project src/WeddingBooking.Api
```

See `CLAUDE.md` for the full layout and the rules that are easy to break by accident.
