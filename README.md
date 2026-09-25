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

Det simulerede regnskab har sin egen `DbContext` og sine egne migrationer i skemaet `accounting_sim`:

```bash
dotnet ef migrations add <Navn> --context AccountingSimDbContext --project src/WeddingBooking.Integrations --startup-project src/WeddingBooking.Api --output-dir Accounting/Simulated/Migrations
dotnet ef migrations script --idempotent --context AccountingSimDbContext --project src/WeddingBooking.Integrations --startup-project src/WeddingBooking.Api
```

Den simulerede udbakke har ligeledes sin egen `SimulatedMailDbContext` i skemaet `mail_sim` (samme kommandoer med `--context SimulatedMailDbContext --output-dir Mail/Simulated/Migrations`).

Læs migrationen igennem, før den køres. Kør aldrig `migrations remove` med `--no-build`: den fjerner så den seneste migration i den gamle build, ikke den nye. SQL'en fra `script --idempotent` er den, der anvendes gennem Supabase-connectoren.

## Tests

```bash
dotnet test tests/WeddingBooking.Domain.Tests
```

Domænetestene dækker pris, betalingsplan og visningsstatus og kører i pipelinen. Integrationstestene i `WeddingBooking.Api.Tests` kører det rigtige API mod en rigtig Postgres og springes over, medmindre `WB_TEST_POSTGRES` peger på en database, der må slettes — de dropper skemaerne `app`, `accounting_sim` og `mail_sim` først:

```bash
WB_TEST_POSTGRES="Host=localhost;Port=5433;Database=weddingbooking;Username=postgres;Password=test" dotnet test tests/WeddingBooking.Api.Tests
```

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
| `OpenAI__Model` | Valgfri, standard `gpt-4.1-mini`. |
| `Summary__StandaloneEnabled` | `true` (standard) åbner `/api/summary/standalone` uden login, rate-begrænset. `false` lukker det. |
| `Trello__ApiKey`, `Trello__Token` | Valgfri. Uden dem importeres fra bordets JSON-eksport. |

Health check: `/health`.
