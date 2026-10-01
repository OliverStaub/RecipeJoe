# Video Import — build tickets

Spec: [../video-import/spec.md](../video-import/spec.md). It links the decision tickets in `../video-import/issues/`.

Implementation tickets are in `issues/`, numbered in dependency order. Work the frontier: any ticket whose blockers are all resolved.

| # | Ticket | Blocked by |
|---|---|---|
| 01 | Background Web Import via API | — |
| 02 | Library shows Imports (Web) | 01 |
| 03 | "Neu" marker | — |
| 04 | Video Import via API (tracer bullet) | 01 |
| 05 | Video Import failures | 02, 04 |
| 06 | Video Import in the UI + E2E | 02, 04 |
| 07 | YoutubeExplode adapter | 04 |
| 08 | Ollama adapter + local dev | 04 |
| 09 | Long-video context guard | 05, 08 |
| 10 | Prompt, German style guide + golden check | 07, 08 |
