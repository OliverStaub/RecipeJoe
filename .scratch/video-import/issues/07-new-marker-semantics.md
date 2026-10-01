# "Neu" marker semantics

Type: grilling
Status: resolved
Blocked by: 05

## Question

When does a Recipe stop being "Neu" in the Library? Options: on first open of its Cook View (prototype default), after a time window, or explicit "mark seen". Decide where that state lives (DB column on Recipe, e.g. `SeenAt`, vs frontend-only/localStorage), whether Web Import Recipes get the marker too, how it interacts with seeding, and the API change (field on `RecipeSummaryDto`, endpoint or side effect that clears it).

## Answer

- Rule: "Neu" clears when the Recipe is first opened in Cook View. No time window, no "mark seen".
- State: nullable `SeenAt` (`DateTimeOffset?`) on Recipe in the database. The migration backfills `SeenAt = CreatedAt` for existing rows.
- Scope: every Import kind (Web + Video). Seeded Recipes show "Neu" too, with no special handling in `seed.sh`.
- API: `IsNew` (bool, `SeenAt == null`) on `RecipeSummaryDto`. `GET /api/recipes/{id}` sets `SeenAt` as a side effect when it is null (user chose this over a separate POST, accepting that a GET changes state).
- Frontend: when the Cook View query succeeds, invalidate the Library query so the badge disappears. E2E: open, go back, badge gone.
- Glossary: **New Recipe** added to `CONTEXT.md`.
