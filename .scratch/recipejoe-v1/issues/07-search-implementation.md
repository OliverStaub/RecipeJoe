# Search implementation

Type: grilling
Status: resolved
Blocked by: —

## Question

How does Library text search over titles + Ingredient Lines work: `ILIKE` vs `pg_trgm` vs Postgres full-text search (language config EN/DE?), server-side vs client-side, search-as-you-type debounce, result ordering. How is it tested (integration against Testcontainers Postgres)?

## Comments

- From [Backend solution layout](04-backend-solution-layout.md): Ingredient Lines are stored as a child table (`OwnsMany`, `Position` + `Text`), not `text[]`/JSONB. Search over them must work against that shape.

## Answer

- **Matching**: Postgres `ILIKE '%token%'`. No `pg_trgm`, no FTS, no `unaccent`, no index (single user, small data; substring matching handles German compounds). Case-insensitive, not accent-insensitive.
- **Where**: server-side, `GET /api/recipes?q=`. Empty or missing `q` returns the whole Library.
- **Tokens**: split `q` on whitespace and AND the tokens. Each token matches the title OR any Ingredient Line (`EXISTS` on the child table). `%`, `_` and `\` in the input are escaped (literal).
- **Ordering**: `CreatedAt` desc, same as the unfiltered Library. No relevance ranking.
- **Response**: summary DTO (`id`, `title`, `hasImage`). No pagination in V1.
- **Frontend**: 250 ms debounce. Query in the URL (`?q=` via React Router search params). TanStack Query `placeholderData: keepPreviousData`.
- **Tests**:
  - Integration (HTTP, Testcontainers): title hit, Ingredient Line hit, tokens split across fields, case-insensitive, no match returns `[]`, `%`/`_` literal, ordering.
  - Frontend unit (Vitest fake timers): debounce, URL sync.
  - E2E: one type-a-query-and-see-it-filter test.
- **Glossary**: Library is now the "searchable list" (filters may come later).
