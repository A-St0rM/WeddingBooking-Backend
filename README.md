# WeddingBooking API

ASP.NET Core (.NET 10) API for bryllupsbookingen på Engestofte Gods. Planen står i `../WeddingBooking/readyprompt.md`.

## Kør lokalt

```bash
dotnet user-secrets --project src/WeddingBooking.Api set "ConnectionStrings:Postgres" "Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.htvujiswzzzlanamefri;Password=<adgangskode>;SSL Mode=Require;Trust Server Certificate=true"
dotnet run --project src/WeddingBooking.Api
```

API'et lytter på `http://localhost:5055`; frontendens dev-server proxyer `/api` dertil. `appsettings.Development.json` peger på Supabase-projektet og tillader `http://localhost:5173`.

## Den første bruger

Adgang kræver en række i `app.users`. Den allerførste oprettes af konfigurationen: en e-mail i `Access:BootstrapAdminEmails` bliver oprettet som `EventManager`, første gang den logger ind. Derefter opretter event manageren de andre under *Brugere*. Fjern bootstrap-e-mailen, når den første bruger findes.

## Migrationer

```bash
dotnet ef migrations add <Navn> --project src/WeddingBooking.Infrastructure --startup-project src/WeddingBooking.Api --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/WeddingBooking.Infrastructure --startup-project src/WeddingBooking.Api
```

Læs migrationen igennem, før den køres. SQL'en fra `script --idempotent` er den, der anvendes gennem Supabase-connectoren.

## Kontrakten

Et build skriver OpenAPI-beskrivelsen til `src/WeddingBooking.Api/WeddingBooking.Api.json`. Den committes; frontenden genererer sine typer fra den med `npm run gen:api`.

## App Service-indstillinger

| Navn | Værdi |
|---|---|
| `ConnectionStrings__Postgres` | Session-pooler-strengen ovenfor, med adgangskoden |
| `Supabase__Url` | `https://htvujiswzzzlanamefri.supabase.co` |
| `Cors__AllowedOrigins__0` | Static Web App'ens adresse, fx `https://icy-dune-06a4f8103.4.azurestaticapps.net` |
| `Access__BootstrapAdminEmails__0` | E-mailen på den første event manager |
| `OpenAI__ApiKey` | Valgfri. Uden den bruger AI-resuméet den faste adapter. |

Health check: `/health`.
