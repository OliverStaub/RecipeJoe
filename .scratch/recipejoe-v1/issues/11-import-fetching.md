# Import fetching

Type: grilling
Status: open
Blocked by: 01

## Question

How does Import fetch a page? Big sites (allrecipes/People Inc, foodnetwork, Cloudflare-protected blogs) refuse a plain fetch. In V1, do we accept that as a failed Import, send browser-like headers, or use a headless browser? Also: timeouts, max page size, redirects, SSRF guard, the user-facing error message per failure kind, and how fetching is faked in tests. See [research](../../../research/schema-org-recipe-variants.md).
