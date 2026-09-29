# 10: Import from the UI → basic Cook View

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.7 (Screens, German copy); decisions: [V1 screens](../../recipejoe-v1/issues/09-v1-screens.md), [Frontend structure and seams](../../recipejoe-v1/issues/14-frontend-structure-and-seams.md)

**What to build:** From the Library, the user opens the Import dialog, pastes a URL and lands on a Cook View showing the Recipe. A failed Import shows a German message inline.

**Blocked by:** 09 (Import a plain Recipe via the API)

**Status:** resolved

- [x] Library header: "Rezepte" + "Importieren" button
- [x] `import` feature: `useImportRecipe()` invalidates the Library; components never import `$api`
- [x] Import `Dialog` with a URL input and a button
  - loading: inputs disabled, spinner, "Wird importiert…", dialog can't be closed
  - failure: destructive `Alert` "Import fehlgeschlagen" + the kind's message, cleared when the URL is edited
  - success: dialog closes, toast "Rezept importiert", navigation to `/recipes/:id`
- [x] Exhaustive `switch` mapping all 7 `ImportFailure` kinds to the German messages in §2.5
- [x] `cook-view` feature: `useRecipe(id)`; the route shows the title, a "Zutaten" list and a "Zubereitung" `ol` with `whitespace-pre-line`; back link to the Library
- [x] Vitest + MSW: failure mapping; dialog loading, error, cleared on edit, and navigation on success
- [x] A tokenised template page in `/fixtures/e2e/`
- [x] E2E: Import → the Cook View shows the tokenised title; one `NoRecipe` Import shows its message
