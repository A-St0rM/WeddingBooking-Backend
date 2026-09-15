# Issue tracker: Local Markdown, in the frontend repo

**There is one tracker for both repos, and it does not live here.** It lives in the sibling repo:

```
../WeddingBooking/.scratch/<feature-slug>/
```

A feature almost always spans both repos — an endpoint and the view that calls it are one piece of work, not two. Splitting the tracker would mean two half-tickets for every feature and no single place that says whether the feature is done.

## Conventions

- One feature per directory: `../WeddingBooking/.scratch/<feature-slug>/`
- The spec is `spec.md` in that directory
- Implementation issues are one file per ticket at `issues/<NN>-<slug>.md`, numbered from `01`
- Triage state is a `Status:` line near the top of each file (see `triage-labels.md`)
- **Every ticket carries a `Repos:` line** naming which repos it touches: `frontend`, `backend`, or both. A ticket touching both stays one ticket and says so.
- Comments append under a `## Comments` heading at the bottom

## When a skill says "publish to the issue tracker"

Write the file under `../WeddingBooking/.scratch/<feature-slug>/`. If that path is not readable from this session, say so rather than creating a second tracker here — a second tracker is worse than no tracker.

## When a skill says "fetch the relevant ticket"

Read it from the same place. The user will normally pass the path or number.
