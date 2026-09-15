# Application tables live in an `app` schema, never in `public`

Supabase publishes the `public` schema as a REST API reachable with the project's anon key — a key that ships in the browser by design. Any table in `public` without row-level security is therefore readable and writable by anyone who opens the site and reads the JavaScript.

ADR-0002 (frontend repo) says we do not use row-level security: the React app never talks to Supabase data, and the .NET API is the single writer with all authorisation in one place. That decision stands, but on its own it only describes how *our* code behaves. It does nothing about Supabase's own front door.

So every table we own lives in an `app` schema, including EF Core's `__EFMigrationsHistory`. The Supabase Data API exposes only the schemas it is configured to expose — `public` and `graphql_public` — so `app` is invisible to it. `public` stays empty.

The alternative was enabling row-level security with no policies on each table. It works, but it is a rule someone has to remember on every new table, across a dozen tickets' worth of tables still to come. Rules that must be remembered get forgotten; a schema boundary does not. The cost is a non-default schema that will surprise anyone who opens the Supabase table editor and finds nothing — which is what this file is for.
