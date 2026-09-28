# 16: Delete a Recipe

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.4, §2.7 (Delete)

**What to build:** The user can delete a Recipe from the Library row menu or the Cook View menu, after confirming.

**Blocked by:** 14 (Complete Cook View), 15 (Library list + search)

**Status:** ready-for-agent

- [ ] `DELETE /api/recipes/{id}`: `204`; `404`; Lines, Steps and image are deleted with the Recipe
- [ ] `useDeleteRecipe()`: invalidates the Library and removes the cached Recipe
- [ ] ⋮ → "Löschen" in Library rows and in the Cook View
  - `AlertDialog` „„{title}" löschen? Das kann nicht rückgängig gemacht werden." with "Löschen" / "Abbrechen"
  - toast "Rezept gelöscht"
  - from the Cook View, go back to the Library
- [ ] Integration: delete + 404
- [ ] Vitest: delete flow
- [ ] E2E: delete from the Library and from the Cook View
