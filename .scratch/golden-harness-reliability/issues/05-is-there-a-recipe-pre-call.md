# 05: "Is there a recipe" pre-call

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-prompting-and-schema-design.md](../research-prompting-and-schema-design.md)

**What to build:** Before extraction, a cheap structured yes/no call decides whether the video text contains a cookable recipe (ingredients or preparation, not just dish names). "No" ends the Import as `NoRecipe`; "yes" proceeds to extraction. The extraction call no longer has to decide that.

**Blocked by:** 02, 03

**Status:** done

- [x] Pre-call has its own short prompt and a tiny strict schema (one boolean); dish-name-only chapter lists and `[Music]`-only transcripts answer "no"
- [x] "No" returns `NoRecipe` without calling extraction
- [x] Provider errors and bad replies in the pre-call map to the same failure kinds as extraction (`LlmUnavailable`, `LlmBadOutput`); timeout and long-video guard still apply
- [x] Unit tests (fake chat client): yes → extraction runs; no → `NoRecipe`, extraction not called; error/bad reply paths
- [x] Model for the pre-call is configurable, defaulting to the extraction model
- [x] Verify: `just golden`; note per-video result in Comments and the extra tokens/cost per Import

## Comments

- Done: `RecipeCheckPrompt.md` + `RecipeCheckReply(bool ContainsRecipe)` (required, strict schema); `RecipeExtractor` asks it first, then extracts. Shared `AskAsync<T>` keeps one failure mapping, timeout per call. Model via `Llm__RecipeCheckModel` (default: `Llm__Model`, set through `ChatOptions.ModelId`). E2E wiremock got a `recipe-check.json` mapping (priority 1, matches `containsRecipe`) so the stubbed LLM answers "yes".
- Golden, gemini-2.5-flash-lite, 3 runs: 1/5/0 recipes pass 8 of 9 video-runs; the only fail was an upstream 429 (`rate-limited upstream`) on the 5-recipe video, correctly reported as `LlmUnavailable`. The `[Music]` video ends after the pre-call (~0.3–1.3 s, no extraction call).
- Cost: the pre-call resends title+description+transcript, so input tokens per Import roughly double; output is a few tokens. At DeepSeek V4 Flash prices ($0.04/M in) negligible; saves one full extraction call on no-recipe videos.
