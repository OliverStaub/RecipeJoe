# 14: Complete Cook View

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.7 (Cook View, German copy); decisions: [V1 screens](../../recipejoe-v1/issues/09-v1-screens.md), [Frontend structure and seams](../../recipejoe-v1/issues/14-frontend-structure-and-seams.md)

**What to build:** The Cook View shows Servings, times and the Source, and keeps the screen on while cooking.

**Blocked by:** 10 (Import from the UI → basic Cook View)

**Status:** ready-for-agent

- [ ] Sticky top bar: back, truncated title, ⋮ `DropdownMenu` with "Quelle öffnen"
- [ ] `Badge`s, each shown only if present:
  - Servings: lucide `Users` icon, text as authored
  - Vorbereitung / Kochen / Gesamt, formatted „1 Std. 15 Min." / „20 Min."
- [ ] `useWakeLock()` → `'active' | 'released' | 'unsupported'`: acquire on mount, re-acquire when the page becomes visible, release on unmount; badge "Bildschirm bleibt an" / "Bildschirmsperre nicht verfügbar"
- [ ] Footer link "Von {host}" to the Source
- [ ] Vitest: time formatting, `useWakeLock` via `vi.stubGlobal`, Cook View with optional parts missing
- [ ] E2E Import → Cook View checks the Wake Lock badge only where supported
