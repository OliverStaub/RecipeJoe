# 11: More golden cases for finer scoring

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-openrouter-model-sweep.md](../research-openrouter-model-sweep.md)

**What to build:** Extend the golden set beyond the 3 videos so pass rates separate models better. Optional; the sweep works without it but scores are coarse (3 cases).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Add golden cases covering varied expected outcomes (no recipe, one recipe, several recipes, long or noisy transcripts)
- [ ] Each case records its expected recipe count and why it was chosen
- [ ] Existing `just golden` still works and its cost per run is stated in the README
- [ ] Sweep estimate assumptions (ticket 08) updated to the new case count

## Comments
