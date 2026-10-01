# Video Import failure kinds

Type: grilling
Status: resolved
Blocked by: 01, 02

## Question

Which ImportFailure kinds does Video Import add (e.g. video unavailable/private, no captions, no recipe found, LLM unreachable/timeout, invalid LLM output), which are retryable, and what German message does each show on the error card?

## Answer

- **Kinds**: reuse shared kinds where the cause matches; add only `NoCaptions`, `LlmUnavailable`, `LlmBadOutput`. The kind names the cause; the Import kind (Web/Video) names the medium, so `ImportDto` gains `kind: Web|Video` (set by the chosen `IImportPath`) and the frontend picks the wording by both.
- **Video source mapping**: network error → `Unreachable`; private/removed/restricted → `NotFound`; bot-block, incl. a caption track that is listed but comes back empty (PO token) → `Blocked`; no caption tracks → `NoCaptions`.
- **Extractor mapping**: network/timeout/5xx/429/401/unknown model → one `LlmUnavailable` (details only in logs); malformed JSON, schema violation, or all Recipes invalid → `LlmBadOutput` (not `BadResponse`); empty `recipes` → `NoRecipe`.
- **Retryability** is per kind, a frontend map (not in the API). Retry: Unreachable, Blocked, BadResponse, LlmUnavailable, LlmBadOutput. Dismiss only: NotFound, NoCaptions, ForbiddenAddress, NoRecipe (a false "no recipe" is a prompt bug, not a retry case).
- **Video messages** (the Web wording stays as is):

| Kind | Message |
|---|---|
| Unreachable | YouTube ist nicht erreichbar. Versuch es später nochmal. |
| Blocked | YouTube blockiert gerade den Zugriff. Versuch es später nochmal. |
| NotFound | Dieses Video ist nicht verfügbar (privat, gelöscht oder eingeschränkt). |
| NoCaptions | Dieses Video hat keine Untertitel, daraus kann kein Rezept gelesen werden. |
| NoRecipe | In diesem Video wurde kein Rezept gefunden. |
| LlmUnavailable | Der LLM-Provider ist nicht erreichbar. |
| LlmBadOutput | Die KI hat keine brauchbare Antwort geliefert. Versuch es nochmal. |

- Partial output (1 of N malformed) stays in the "LLM output validation" fog; ≥1 saved = success.
