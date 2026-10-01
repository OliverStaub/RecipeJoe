# 12: Golden check on OpenRouter

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · follows [10](10-prompt-style-guide-golden-check.md)

**What to build:** `just golden` judges prompt changes against OpenRouter, since Ollama is gone. It stays opt-in and out of CI. Ollama wording is removed from the golden check and its docs.

**Blocked by:** 11

**Status:** ready-for-agent

- [ ] `just golden` runs the extractor on the recorded videos via OpenRouter (same `Llm__*` config as the API); fails clearly if the key is missing
- [ ] Ollama wording removed from the golden test docs, the MSTest settings note and the justfile comment
- [ ] Ticket 10 text updated: "real Ollama" becomes OpenRouter
- [ ] Existing recordings and expectations unchanged and still checked
- [ ] Manually verified: `just golden` run end to end; result noted in Comments
