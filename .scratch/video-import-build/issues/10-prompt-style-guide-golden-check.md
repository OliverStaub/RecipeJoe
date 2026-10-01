# 10: Prompt, German style guide + golden check

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [LLM extraction seam](../../video-import/issues/04-llm-extraction-seam.md)

**What to build:** Video Import writes Recipes that read like proper German recipes: clear Ingredient Lines, ordered Steps, one Recipe per dish, no invented Servings or timings, even from English or noisy transcripts. An optional local golden check runs the extractor against OpenRouter on the candidate videos, so prompt changes can be judged before committing.

**Blocked by:** 07, 08

**Status:** ready-for-agent

- [ ] German style guide in the embedded prompt (recipe style, not literal translation; units and wording conventions; one entry per dish; null Servings/timings unless stated; ignore ASR noise)
- [ ] Golden check: an opt-in local command (not part of `just test`, never in CI) runs the extractor on recorded transcripts of i84Sc5uvQa8, 6wR2T-PexT4 (long DE, noisy start) and 6tMZNYQkycI against OpenRouter and reports per video: Recipe count, titles, Ingredient Line/Step counts, null fields
- [ ] Expectations recorded per video (e.g. recipe count, German output) and checked by the command; how "good" is judged noted in the ticket's Comments
- [ ] 6tMZNYQkycI (transcript is only `[Music]`, recipes exist only as on-screen text) ends as `NoRecipe`: the prompt makes the LLM return an empty list when the text holds no recipe; golden expectation records `NoRecipe`
- [ ] Extractor unit tests still pass with the final prompt (they don't assert prompt text)
