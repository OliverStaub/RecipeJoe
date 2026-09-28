# 13: Recipe images

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.3, §2.4, §2.5 (Images); decision: [Recipe image storage](../../recipejoe-v1/issues/06-recipe-image-storage.md)

**What to build:** An imported Recipe keeps its image and the Cook View shows it. A failed image download never fails the Import.

**Blocked by:** 10 (Import from the UI → basic Cook View)

**Status:** ready-for-agent

- [ ] Parser image candidate: resolve `@id` → first array element → `url ?? contentUrl` → resolve against the page URL
- [ ] 1:1 `RecipeImages` table (`bytea`, `ContentType`) + migration; cascade delete; not loaded with the Recipe
- [ ] The Importer downloads the first candidate via `IPageFetcher`
  - ≤ 5 MB; jpeg/png/webp/gif by magic bytes; stored as-is
  - any failure → Recipe saved without an image, failure logged
- [ ] `GET /api/recipes/{id}/image`: stored bytes + type, `Cache-Control: public, max-age=31536000, immutable`; `404` if there is none
- [ ] The backend sets `imageUrl` in the Recipe DTO; `hasImage` is ready for the summary
- [ ] Fixture placeholder images (mostly JPEG; one PNG, one WebP, one GIF); one Recipe without an image, one with a 404 image URL
- [ ] Cook View shows the image in a 4:3 `AspectRatio`, omitted if there is none
- [ ] Unit: oversize and bad magic bytes (generated in tests), 404, no image
- [ ] Integration: image endpoint bytes, type, cache header, 404
