# Recipe image storage

Type: grilling
Status: resolved
Blocked by: —

## Question

Where are downloaded Recipe images stored — Postgres `bytea` vs a filesystem volume — and how are they served to the frontend? Also: what happens on Import when the image download fails (save the Recipe without image vs fail the Import), size/format limits, resizing or not.

## Comments

- From [docker-compose topology](05-docker-compose-topology.md): if filesystem storage is chosen, add a named volume to the `backend` service in `compose.yaml` (wiped by `just reset`). → Not needed: `bytea` chosen.

## Answer

- **Storage**: Postgres `bytea` in a separate 1:1 `RecipeImages` table (`RecipeId` PK/FK, `ContentType`, `Bytes`), cascade-deleted with the Recipe. Saved in the same transaction as the Recipe. No filesystem volume. No image-store interface; `DbContext` directly. Entity + endpoint in `Images/`.
- **Candidate**: first `image` URL only (per parser algorithm); no fallback to later candidates.
- **Download**: via `IPageFetcher`. SSRF guard/timeouts per [Import fetching](11-import-fetching.md).
- **Limits**: ≤ 5 MB (streamed, abort past cap); `image/jpeg|png|webp|gif`, verified by magic bytes, not just `Content-Type`.
- **Failure** (no image, unreachable, 404, too big, bad format): save the Recipe without image. Logged, no user-facing warning. Never fails the Import.
- **No resizing / re-encoding**: stored as downloaded; display via shadcn `AspectRatio` + `object-fit`.
- **Serving**: `GET /api/recipes/{id}/image` → stored bytes + stored content type, `Cache-Control: public, max-age=31536000, immutable`; `404` if none. Recipe DTO has nullable `imageUrl` set by the backend. Frontend uses a plain `<img>`.
- **Tests**:
  - Unit: fixture fetcher returns image bytes; cases for oversize, bad magic bytes, 404, no image → Recipe saved without image.
  - Integration: endpoint bytes, content type, cache header; 404 when there's no image.
  - E2E: `fixtures` nginx serves a sample image.
