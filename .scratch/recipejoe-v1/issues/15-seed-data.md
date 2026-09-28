# Seed data

Type: grilling
Status: resolved
Blocked by: 05, 11

## Question

How do local dev and E2E get Recipes to work with? Candidate: capture real pages from chefkoch.de and migusto.ch as saved HTML, then seed by running Import against the fixtures site (so seeding exercises the real parser) rather than inserting rows directly. Decide: which/how many Recipes, where the HTML lives (shared with the Import fixtures?), how they're captured (one-off script vs by hand), when seeding runs (`just seed`? on `up`? per E2E test vs per run), whether E2E relies on seed data or imports its own, and how images are served.

## Comments

## Answer

- **Corpus**: 30 synthetic Recipes committed under `/fixtures/recipes/`, generated once as static HTML (no generator script). Each file opens with a comment naming the variant(s) it exercises. No third-party content is committed; the research's real-page fixture list and a capture script are dropped.
  - ~18 plain German Recipes (varied dishes and ingredients, for search and scrolling).
  - ~12 variant pages, 2–3 of them in English: `@graph` + `@id` refs, top-level array, `@type: ["Recipe"]`, HowToSection(s), string[] instructions, single blob string, HowToStep `name`, non-ISO / `P0Y…` / `PT01H15M` durations, numeric / array / "4 to 6" yield, entities and `<br>`, Recipe in a later block plus one malformed block, multi-recipe page (first wins).
  - `failing/` (not seeded): no-recipe, microdata-only, broken-json-only.
  - Images are tiny generated placeholders (solid colour + dish name): mostly JPEG, one each of PNG, WebP and GIF. One Recipe has no image; one has an image URL that 404s (saved without image). The >5 MB and wrong-type image cases are generated inside the tests, not committed.
- **Consumers**: parser unit tests via a csproj link (`../../fixtures/**`, copied to output); the fixture `IPageFetcher` adapter; the `fixtures` nginx container via a read-only bind mount (`./fixtures:/usr/share/nginx/html:ro`); dev seed. CI gets the files through checkout: no secrets, no network.
- **E2E**: never relies on seed data. `fixtures/e2e/` template pages put `__TOKEN__` in the title; nginx `sub_filter '__TOKEN__' $arg_t` swaps it, so each test imports `http://fixtures/e2e/<page>.html?t=<uuid>` and gets a unique title and search term (keeps the unique-title rule from [docker-compose topology](05-docker-compose-topology.md)).
- **Dev seed** (dev only):
  - `just seed` (bash + curl + jq): `GET /api/recipes`; if the Library isn't empty, it exits with a message.
  - Otherwise it runs `POST /api/recipes/import` for every corpus page except `failing/`, with `url=http://localhost:8081/recipes/<name>.html`.
  - It prints one ✓/✗ line per page with the `ImportFailure` kind and exits non-zero if any page failed (that signals a parser regression).
  - It never runs automatically. To start fresh: `just reset && just dev-db && just seed`.
  - No real-URL seeding (`seed-urls.txt` dropped).
- **Compose/config change**: `fixtures` moves **out of** `profile: app`, so `just dev-db` starts it too, bound to `127.0.0.1:8081`. `Import:AllowedHosts=localhost` is set in `appsettings.Development.json` only (E2E keeps `fixtures`, integration keeps loopback).
