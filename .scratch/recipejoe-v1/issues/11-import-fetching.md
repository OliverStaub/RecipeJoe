# Import fetching

Type: grilling
Status: resolved
Blocked by: 01

## Question

How does Import fetch a page? Big sites (allrecipes/People Inc, foodnetwork, Cloudflare-protected blogs) refuse a plain fetch. In V1, do we accept that as a failed Import, send browser-like headers, or use a headless browser? Also: timeouts, max page size, redirects, SSRF guard, the user-facing error message per failure kind, and how fetching is faked in tests. See [research](../../../research/schema-org-recipe-variants.md).

## Comments

- From [docker-compose topology](05-docker-compose-topology.md): E2E Imports go through the real HttpClient to a `fixtures` container on the compose network (private IP). The SSRF guard needs a configurable allowlist (e.g. host `fixtures`) enabled only in the E2E environment.
- From [Recipe image storage](06-recipe-image-storage.md): image downloads go through the same `IPageFetcher` and must use the same SSRF guard. They need a 5 MB streamed cap, and the fetcher should return the raw bytes and `Content-Type`. Pick the timeouts here.
- From [V1 screens](09-v1-screens.md): the Import dialog shows an inline error with a title + detail. The prototype split it into invalid URL / couldn't load page (e.g. 403) / no Recipe found. Decide the error categories the API returns (e.g. problem-details `type`) so the UI can word them.

## Answer

- **Bot-blocking**: send browser-like headers (Chrome UA, `Accept`, `Accept-Language: de-DE,de;q=0.9,en;q=0.8`, gzip/br). Headless browser and a "paste HTML" fallback are out of scope. Blocked sites fail with `Blocked`.
- **Limits**: page 10 s total timeout, 5 MB streamed cap; image 10 s, 5 MB. Redirects are followed by hand (`AllowAutoRedirect=false`), max 5, http/https only. No retries. Automatic gzip/brotli decompression. The page must be `text/html` or `application/xhtml+xml`. Charset comes from the header, falling back to the meta tag (AngleSharp).
- **SSRF guard**: checks the IP in `SocketsHttpHandler.ConnectCallback` on the address actually connected to, so it covers redirects, image downloads and DNS rebinding. It allows only http/https and rejects loopback, RFC1918, link-local (incl. 169.254.169.254), CGNAT, multicast, unspecified, and IPv6 ULA/link-local. Config `Import:AllowedHosts` bypasses the check and is set only in E2E (`fixtures`) and integration tests (loopback).
- **Failure kinds** (`ImportFailure`, a typed enum in the OpenAPI contract; the API returns only the kind, and the frontend maps it to German text):

  | Kind | Trigger | UI message (de, „du") |
  |---|---|---|
  | `InvalidUrl` | not absolute http(s) | „Das ist keine gültige Webadresse." |
  | `Unreachable` | DNS/connection failure, timeout | „Die Seite ist nicht erreichbar. Prüfe die Adresse und versuch es nochmal." |
  | `Blocked` | 401/402/403/429, or a challenge page (`cf-mitigated: challenge` / "Just a moment…") | „Diese Seite blockiert automatische Zugriffe und kann nicht importiert werden." |
  | `NotFound` | 404/410 | „Diese Seite existiert nicht." |
  | `BadResponse` | other non-2xx, not HTML, too large, too many redirects | „Die Seite hat etwas geliefert, das wir nicht lesen können." |
  | `ForbiddenAddress` | SSRF guard rejects | „Diese Adresse ist nicht erlaubt." |
  | `NoRecipe` | parser failure (see [Schema.org Recipe variants](01-schema-org-recipe-variants.md)) | „Auf dieser Seite wurde kein Rezept gefunden." |

- **Tests**: `IPageFetcher` fixture adapter for unit and integration tests (already decided). The HttpClient adapter gets integration tests against WireMock.Net on loopback (allowlisted) for status mapping, redirects, caps and timeouts. The IP classifier is a pure `IsPublic(IPAddress)` with table-driven unit tests. One integration test covers loopback *without* the allowlist → `ForbiddenAddress`.
- **Surfaced**: the UI is **German only**, with strings hard-coded and no i18n library. Code, API and glossary stay English. This changes the English copy in [V1 screens](09-v1-screens.md).
