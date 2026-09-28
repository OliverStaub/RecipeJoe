# Search implementation

Type: grilling
Status: open
Blocked by: —

## Question

How does Library text search over titles + Ingredient Lines work: `ILIKE` vs `pg_trgm` vs Postgres full-text search (language config EN/DE?), server-side vs client-side, search-as-you-type debounce, result ordering. How is it tested (integration against Testcontainers Postgres)?

## Comments

- From [Backend solution layout](04-backend-solution-layout.md): Ingredient Lines are stored as a child table (`OwnsMany`, `Position` + `Text`), not `text[]`/JSONB. Search over them must work against that shape.
