# Import fetch policy — spec

**Status:** ready-for-agent

**Origin:** architecture review 2026-09-30, candidate 2. Terms follow [`CONTEXT.md`](../../CONTEXT.md); architecture terms follow `/codebase-design` (module, interface, seam, adapter, depth, locality, leverage).

## Problem Statement

When an Import fails, the cook is told why: the site blocked us, the page doesn't exist, it isn't a recipe page, and so on. The rules that turn a web response into one of those reasons (403/429 → Blocked, Cloudflare challenge → Blocked, non-HTML → BadResponse, over the size cap → BadResponse, …) live only inside the production HTTP adapter.

The fetch seam has two adapters (HTTP in prod, fixtures in tests), but the fixture adapter only knows "found" or "not found". So no Importer or endpoint test can produce Blocked or BadResponse, and the failure path the cook sees for those cases is only covered by the WireMock tests of the HTTP adapter itself. The seam also has two near-identical methods (page and image), and every test stub has to throw on the one it doesn't expect. The image downloader repeats a size check that only matters because the fixture adapter skips the policy.

## Solution

Adapters only move bytes: they return the raw response (status, relevant headers, body, final URL) or a transport failure. The Import module owns the fetch policy: it classifies every raw response into a success or an Import failure, the same way for both adapters. The seam shrinks to one method. The fixture adapter learns to serve arbitrary statuses and headers, so tests can reach every failure kind through the Importer and through the API.

The cook sees no change; the failure kinds and API responses stay exactly the same.

## User Stories

1. As a cook, I want an Import from a site that blocks us to say so, so that I know retrying won't help (behaviour preserved).
2. As a cook, I want an Import from a Cloudflare challenge page to say the site blocked us, so that I'm not told the page has no recipe (preserved).
3. As a cook, I want an Import of a URL that returns a non-HTML page to fail with a clear reason, so that I know the link is wrong (preserved).
4. As a cook, I want an Import of an oversized page to fail cleanly, so that the app never hangs on huge downloads (preserved).
5. As a cook, I want a missing or oversized image to leave the Recipe without an image rather than fail the Import (preserved).
6. As a cook, I want the same failure reason no matter whether the problem is detected before or after redirects, so that messages are consistent.
7. As a developer, I want the response-classification rules in the Import module, so that changing a rule touches one place and applies to every fetch.
8. As a developer, I want the fetch seam to have one method, so that adapters and test doubles are trivial to write.
9. As a developer, I want the fixture adapter to go through the same classification as production, so that tests exercise the real policy.
10. As a developer, I want to make the fixture adapter serve a given status, headers and body for a URL, so that I can test Blocked, NotFound and BadResponse through the Importer.
11. As a developer, I want the same canned responses usable from API-level tests, so that I can check the HTTP status and problem body for each failure kind end to end.
12. As a developer, I want the HTTP adapter to keep transport concerns only (redirects, SSRF guard, timeout, capped streaming, request headers), so that its tests focus on transport.
13. As a developer, I want the size cap applied by the policy to every body, so that an adapter that hands over a whole body cannot bypass it.
14. As a developer, I want the image downloader's duplicate size check removed, so that the cap lives in one place.
15. As a developer, I want the ad-hoc stub fetchers in Importer and image downloader tests replaced by the fixture adapter, so that there is one test adapter.
16. As a developer, I want the WireMock tests to keep asserting failure kinds, so that the real HTTP path plus policy stays covered end to end.
17. As a developer, I want the challenge detection to look at the body the policy already has, so that there is no second, separate UTF-8 read in the adapter.

## Implementation Decisions

- **Fetch seam (reshaped):** one method taking a URL and a fetch kind (page or image). The kind only changes the request's Accept header in the HTTP adapter. It returns either:
  - a raw response: status code, Content-Type, the challenge-mitigation header value, the body (bytes, or "over the cap" when the adapter stopped reading), and the final URL after redirects; or
  - a transport failure: Unreachable (DNS, connect, TLS, our timeout), ForbiddenAddress (SSRF guard), BadResponse (too many redirects, redirect without location, redirect to a non-HTTP scheme).
- **HTTP adapter keeps:** redirect following, SSRF guard in the connect callback, total timeout, request headers, Content-Length precheck and capped streaming (reports "over the cap" rather than reading on). It always reads the body (capped), for every status, so the policy can inspect error pages.
- **Fetch policy (new, inside the Import module):** wraps the seam and is what the Importer and the image downloader call. It exposes the current "fetch → Result of fetched content or Import failure" shape per kind. Rules, moved unchanged:
  - challenge-mitigation header says challenge → Blocked
  - 401, 402, 403, 429 → Blocked
  - 404, 410 → NotFound
  - 503 whose body has the challenge marker → Blocked
  - any other non-2xx → BadResponse
  - body over the cap (reported by the adapter, or longer than the cap) → BadResponse
  - page kind: Content-Type missing or not HTML/XHTML → BadResponse
  - page kind: 2xx body with the challenge marker → Blocked
  - otherwise → fetched content (bytes, Content-Type, final URL)
- The fetch policy is the only consumer of the seam. Its classification helpers are private to it.
- **Image downloader:** calls the fetch policy with the image kind; its own size check is deleted (magic-byte sniffing stays).
- **Fixture adapter:** keeps serving fixture files and the link-shortener redirect; gains per-URL canned raw responses (status, headers, body) that a test can register. Unregistered, unknown URLs still 404.
- `ImportFailure` kinds, the endpoint's status mapping and the API contract are unchanged. No new glossary terms (the fetch policy is technical, not domain language).

## Testing Decisions

- Good tests go through the highest interface: the Importer for Import behaviour, the API for status codes and problem bodies. They assert failure kinds and resulting Recipe Drafts, not how classification is implemented.
- Seams: the Importer (unit tests, fixture adapter) and the API (integration tests, fixture adapter via the test API factory). No new seam; the fetch seam keeps two adapters.
- Add Importer tests, one per classification rule, using canned fixture responses (Blocked via 403, via 429, via challenge header, via 503 challenge page, via 200 challenge page; NotFound via 410; BadResponse via 500, non-HTML page, over-cap body).
- Add at least one API test for Blocked and one for BadResponse, asserting the status code and problem `kind`.
- WireMock tests of the HTTP adapter keep their failure-kind assertions but resolve the fetch policy from the Import service registration, so they cover real transport plus policy. Transport-only cases (redirects, SSRF incl. redirect to a private IP, timeout, streaming cap, Accept header) stay there.
- Image downloader tests drop the over-cap cases that tested its removed check; the policy's cap is covered by the Importer tests.
- Stub fetchers in Importer and image downloader tests are replaced by the fixture adapter.
- Prior art: `ImporterTests`, `HttpPageFetcherTests` (WireMock via the Import service registration), `ImportRecipeTests` (API), the fixture adapter and test API factory. Mutation testing (Stryker) is available locally for the policy rules.

## Out of Scope

- Dissolving the Images namespace into Import and Library, and removing its dependency cycles (candidate 3), beyond deleting the duplicate size check.
- Building the image URL in one place (candidate 6).
- Moving decoding into the parser / charset-aware corpus tests (candidate 7).
- New failure kinds, new retry behaviour, headless fetching.
- Any frontend change.

## Further Notes

- The challenge marker check currently decodes the first 4 KB as UTF-8; keep that behaviour, just run it in the policy.
- Reading bodies of error responses (capped) is a small behaviour change in the HTTP adapter; the cap keeps it bounded.
