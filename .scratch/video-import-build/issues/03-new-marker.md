# 03: "Neu" marker

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: ["Neu" marker semantics](../../video-import/issues/07-new-marker-semantics.md)

**What to build:** Recipes that haven't been opened in Cook View yet (New Recipes) show a "Neu" badge in the Library. The first time the cook opens one in Cook View, the badge disappears from the Library. Existing Recipes aren't marked after the upgrade.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Nullable `SeenAt` on Recipe; the migration backfills `SeenAt = CreatedAt`; the pending-model-changes contract test passes
- [ ] `IsNew` on the Recipe summary (true when `SeenAt` is null)
- [ ] Getting one Recipe sets `SeenAt` when it's null; later gets leave it unchanged
- [ ] shadcn `Badge` "Neu" next to the title in the Library row
- [ ] A successful Cook View load invalidates the Library, so the badge is gone on return
- [ ] Seeded Recipes show "Neu" (no seed-script changes)
- [ ] API tests: new Recipe is new; it isn't after a get; backfilled rows aren't
- [ ] Route-level test: badge shown, gone after opening the Cook View and going back
- [ ] E2E: open, go back, badge gone
- [ ] Coverage gate passes
