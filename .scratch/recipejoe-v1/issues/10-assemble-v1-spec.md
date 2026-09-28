# Assemble V1 spec

Type: task
Status: resolved
Blocked by: 01, 02, 03, 04, 05, 06, 07, 08, 09, 11, 12, 13, 14, 15

## Question

Consolidate the map's standing decisions and every resolved ticket into `.scratch/recipejoe-v1/spec.md`, split into a tooling phase and an MVP phase, so implementation tickets can be generated from it.

## Comments

- From [Import fetching](11-import-fetching.md): the UI is German only, with strings hard-coded and no i18n library. The English copy in [V1 screens](09-v1-screens.md) (buttons, toasts, delete dialog, headings) must be German. Import failure kinds map to German messages in the frontend.
- From [Step titles and Servings parsing](12-step-titles-and-servings-parsing.md): parser rules for HowToStep `name` (dropped), `recipeYield` selection (free text), and newline normalisation within Steps.
- From [Local automation choices](13-local-automation-choices.md): `just` gains `fmt-check` and `setup`; root `.prettierrc`; Markdown is not Prettier-formatted.
- From [CI workflow design](08-ci-workflow-design.md): new `just build` recipe; `test-contract` also runs EF `has-pending-model-changes`; coverage gate keyed on the `CI` env var; NuGet lock files + `--locked-mode`; `.nvmrc`; no Playwright browser cache; Renovate config (SHA pins, automerge minor/patch, 3-day release age).
- From [Frontend structure and seams](14-frontend-structure-and-seams.md): new dev dependency MSW; root `/e2e` Playwright package (`just test-e2e` runs it); Vite dev proxy for `/api`.

- From [Seed data](15-seed-data.md): overrides earlier details. The Import fixtures are a synthetic corpus at repo root `/fixtures/recipes/`, not real saved pages under `backend/tests/Fixtures/` (overrides the [research](../../../research/schema-org-recipe-variants.md) §4 list and the path in [Backend solution layout](04-backend-solution-layout.md)). The `fixtures` compose service is no longer under `profile: app` (overrides [docker-compose topology](05-docker-compose-topology.md)). Development config allowlists `localhost`.

## Answer

Spec written: [`spec.md`](../spec.md). It is split into Architecture (§2), Quality gates (§3), Phase 1 Tooling with 9 slices (§4) and Phase 2 MVP with 12 slices (§5). Gaps and conflicts found while assembling are listed in §6. The user confirmed:

- Times are stored as `interval` and exposed as integer minutes (`prepMinutes` etc.); the frontend formats them in German.
- `cov-backend` / `cov-frontend` recipes: each CI test job runs its tests once, with coverage.
- `RecipeSummary` gains `sourceUrl` (the Library row shows the Source host).
- The German copy table (§2.7) is accepted.
- Also added: `DELETE /api/recipes/{id}`. `ImportFailure` has 7 kinds in a `kind` field (`InvalidUrl` → 400, the rest → 422).
