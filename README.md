# WeddingBooking API

## Run it

```bash
dotnet run --project src/WeddingBooking.Api
```

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

`WeddingBooking.Api.Tests` needs Docker running — it starts a real Postgres in a container.

## Secrets

None are committed. Set them locally with:

```bash
dotnet user-secrets set "Economic:AppSecretToken" "..." --project src/WeddingBooking.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "..." --project src/WeddingBooking.Api
```

See `CLAUDE.md` for the full layout and the rules that are easy to break by accident.
