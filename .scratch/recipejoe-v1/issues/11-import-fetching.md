# Import fetching

Type: grilling
Status: claimed
Blocked by: 01

## Question

How does Import fetch a page? Big sites (allrecipes/People Inc, foodnetwork, Cloudflare-protected blogs) refuse a plain fetch. In V1, do we accept that as a failed Import, send browser-like headers, or use a headless browser? Also: timeouts, max page size, redirects, SSRF guard, the user-facing error message per failure kind, and how fetching is faked in tests. See [research](../../../research/schema-org-recipe-variants.md).

## Comments

- From [docker-compose topology](05-docker-compose-topology.md): E2E Imports go through the real HttpClient to a `fixtures` container on the compose network (private IP). The SSRF guard needs a configurable allowlist (e.g. host `fixtures`) enabled only in the E2E environment.
- From [Recipe image storage](06-recipe-image-storage.md): image downloads go through the same `IPageFetcher` and must use the same SSRF guard. They need a 5 MB streamed cap, and the fetcher should return the raw bytes and `Content-Type`. Pick the timeouts here.
- From [V1 screens](09-v1-screens.md): the Import dialog shows an inline error with a title + detail. The prototype split it into invalid URL / couldn't load page (e.g. 403) / no Recipe found. Decide the error categories the API returns (e.g. problem-details `type`) so the UI can word them.
