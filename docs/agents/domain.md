# Domain Docs

**The domain documentation is not in this repo.** It lives in the sibling frontend repo and is shared by both. Do not duplicate it here — two copies drift apart within a week.

## Before exploring, read these

- `../WeddingBooking/CONTEXT.md` — the glossary
- `../WeddingBooking/ARCHITECTURE.md` — data model, system shape, which repo owns what
- `../WeddingBooking/docs/adr/` — cross-cutting decisions
- `docs/adr/` **in this repo** — backend-only decisions

If those paths are not readable from this session, say so and ask the user to grant access to the sibling directory. Do not proceed by guessing at the vocabulary, and do not recreate the glossary here.

## Use the glossary's vocabulary

When your output names a domain concept — an endpoint name, a class, a test name, a ticket title — use the term as `CONTEXT.md` defines it. Don't drift to a synonym it lists under _Avoid_. Danish domain terms keep their Danish spelling in code; file and folder paths are ASCII-folded (ADR-0001).

If a concept you need isn't in the glossary, that's a signal: either you're inventing language the project doesn't use, or there's a real gap worth recording with `/domain-modeling`.

## Flag ADR conflicts

If your work contradicts an ADR in either repo, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0005 (payment status is never stored), but worth reopening because…_
