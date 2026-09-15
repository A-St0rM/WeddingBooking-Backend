# WeddingBooking — API

ASP.NET Core Web API for the wedding booking system. The React frontend lives in the sibling repo `WeddingBooking`.

## The domain docs live in the frontend repo

This repo deliberately does not duplicate them. Read them there before working here:

- `../WeddingBooking/CONTEXT.md` — the glossary. Use its terms; do not drift to synonyms it lists under _Avoid_.
- `../WeddingBooking/ARCHITECTURE.md` — data model, system shape, build order.
- `../WeddingBooking/docs/adr/` — the decisions and why. Surface it explicitly if your work contradicts one.
- `../WeddingBooking/.scratch/` — specs and issues.

`docs/adr/` in *this* repo is for decisions that are backend-only.

## Projects

| Project | Holds |
|---|---|
| `WeddingBooking.Domain` | Entities and the two pure calculations: pricing and status derivation. No dependencies on anything. |
| `WeddingBooking.Infrastructure` | Postgres via EF Core, Supabase Storage. |
| `WeddingBooking.Integrations` | e-conomic, Trello, mail drafts, summarisation — each an interface with a Fake and a Real implementation. |
| `WeddingBooking.Api` | Controllers, auth, access control, DTOs. The only writer. |
| `tests/WeddingBooking.Api.Tests` | **Primary seam.** Boots the real app through `WebApplicationFactory` and calls real endpoints. |
| `tests/WeddingBooking.Domain.Tests` | **Narrow seam.** Pricing and status derivation as pure functions. |

Those two are the only seams. No repository tests, no controller unit tests with mocked services.

## Fakes are production code

`Integrations:{Economic,Trello,Mail,Summarisation}` selects `Fake` or `Real` per integration. The fakes are how the whole system runs for a demo with no credentials — not test scaffolding. Never branch on environment inside a real adapter; add a fake instead.

## Configuration

`appsettings.json` documents the shape of every setting and contains **no secrets in any environment**. Locally, secrets go in user-secrets:

```bash
dotnet user-secrets set "Economic:AppSecretToken" "..." --project src/WeddingBooking.Api
```

In Azure they are App Service application settings or Key Vault references. `appsettings.Demo.json` runs everything faked.

## Rules that are easy to break by accident

- **Never persist anything derivable.** Line totals, the Booking total, "klar til afvikling", the displayed status, and above all **payment status** are computed on read. Payment status is read from e-conomic and never written to our database — ADR-0005.
- **`Gæsteantal` exists once**, on the Booking. Everything per-guest reads it.
- **Access control lives in the API, in one place.** Not in React, not in Postgres row policies — ADR-0002.
- **Trello is written to, never read** — ADR-0006.

## What belongs here, and what belongs in the frontend

The split is by *responsibility*, not by convenience. The canonical version of this table is in `../WeddingBooking/ARCHITECTURE.md`.

| Concern | Lives in |
|---|---|
| Pricing, status derivation, any calculation over money | **Backend** — `Domain`. Never duplicated in the frontend. |
| Access control | **Backend** — `Api`, in one place |
| Database schema and migrations | **Backend** — `Infrastructure` |
| e-conomic, Trello, mail drafts, summarisation | **Backend** — `Integrations` |
| Document generation (Aftalebekræftelse → PDF) | **Backend** |
| Routing, forms, layout, responsiveness | **Frontend** |
| Danish UI text | **Frontend** |
| Glossary, ADRs, specs, tickets | **Frontend repo**, as documentation only |

The test for a disputed piece: **if getting it wrong would produce a wrong number or leak data, it belongs here.** The frontend renders what this API tells it and calculates nothing of consequence.

A change that needs both repos is still **one ticket**, with a `Repos:` line naming both.

## Agent skills

### Issue tracker

One tracker for both repos, in the sibling frontend repo at `../WeddingBooking/.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical roles, each label string equal to its name. See `docs/agents/triage-labels.md`.

### Domain docs

Shared with the frontend repo: glossary and cross-cutting ADRs live in `../WeddingBooking/`; `docs/adr/` here is backend-only. See `docs/agents/domain.md`.
