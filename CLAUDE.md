# WeddingBooking — API

ASP.NET Core-API'et til bryllupsbookingen på Engestofte Gods. Frontenden og projektdokumentationen ligger i søsterrepoet `../WeddingBooking`.

**Før du designer eller bygger en feature, læs `../WeddingBooking/readyprompt.md`** — arkitektur, datamodel, porte og faserne med deres færdig-kriterier. Kravene og forretningsreglerne står i `../WeddingBooking/projekt-prompt.md`. Kan sessionen ikke læse søstermappen, så sig det og bed om adgang; arbejde på tværs af begge repoer køres fra `~/Repos`.

## Projekterne

`Domain` afhænger af intet og rummer entiteter, regler, de rene beregninger og portene. `Infrastructure` er EF Core og filer. `Integrations` er adapterne. `Api` er tynde controllers, adgangskontrol og DTO'er.

## Regler, der er lette at bryde

- **Udregn, når der læses.** Linjetotaler, bookingens total, raternes beløb, "klar til afholdelse" og visningsstatus gemmes ikke. Gæsteantallet findes ét sted: på bookingen.
- **Betalingsstatus læses gennem `IAccountingSystem`** hver gang. Bookingtabellerne gemmer kun referencer — kunde-, tilbuds- og fakturanumre.
- **En regel, der afgør et beløb eller en status, bor i `Domain`** som en ren funktion, så den kan testes uden database.
- **Hvert endepunkt kræver login** via fallback-politikken. Kun `/health`, de offentlige forespørgsler, det selvstændige resumé og Trellos webhook (som kræver Trellos signatur) melder fra. En indlogget uden række i `users` får 403 fra adgangs-middlewaren.
- **Pris- og betalingsfelter fjernes fra svaret** for rollerne `Chef` og `EventProducer`.
- **Tabeller oprettes i skemaet `app`.** Supabase eksponerer `public` for alle med den offentlige nøgle.
- **Læs hver genereret migration igennem, før den køres.** Flyttes data, skrives den i hånden: opret, tilføj nullable, flyt data, stram til, fjern det gamle.
- **En mail sendes først, når Johan har set teksten og trykket Send.**
- **Hemmeligheder ligger i `dotnet user-secrets` lokalt og i App Service-indstillingerne i Azure.** `appsettings.json` viser kun formen.
- **Koden er på engelsk.** Oversættelsen af domænebegreberne står i `readyprompt.md` §7.

## Databasen

Supabase-projektet `WeddingBooking` (`htvujiswzzzlanamefri`, eu-central-1) er oprettet og klar. **Du har adgang til Supabase-connectoren** — brug den til at se skemaet, før du ændrer det, til at anvende migrationer og til at køre advisors bagefter.

Skemaet ejes af EF Core-migrationerne. Skriv ændringen som en EF-migration, og anvend den SQL, `dotnet ef migrations script --idempotent` genererer — så EF's historik og databasen er enige. Fremgangsmåden står i `readyprompt.md` §8.

## Middleware-rækkefølgen

`UseForwardedHeaders` → `UseHttpsRedirection` → `UseCors` → undtagelses-middleware → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → adgangs-middleware.

Undtagelses-middlewaren ligger efter `UseCors`, fordi ASP.NET fjerner CORS-headerne, når en undtagelse slipper igennem — og så viser browseren en 500 som en CORS-fejl.

## Deployment

- Databasen nås via Supabases **session-pooler**; den direkte vært har kun IPv6. Forbindelsesstrengen står i `readyprompt.md` §8.
- App Service-indstillingen hedder `ConnectionStrings__Postgres`. Kontrollér værdien efter indsættelse.
- `/health` er anonym, fordi App Service's health check ellers genstarter appen i ring.
- Migrationer: `dotnet ef database update --project src/WeddingBooking.Infrastructure --startup-project src/WeddingBooking.Api`.
- Hovedbranchen hedder `main` med lille m. Én feature-branch pr. fase, med samme navn som i frontend-repoet.
