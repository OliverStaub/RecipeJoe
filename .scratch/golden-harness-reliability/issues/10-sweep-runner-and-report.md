# 10: Sweep runner and cost/quality report

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-openrouter-model-sweep.md](../research-openrouter-model-sweep.md)

**What to build:** A script you run yourself (`just sweep`) that runs the candidate list through the golden cases in two stages and writes a report showing the best trade-off between score and cost.

**Constraint:** The agent has no access to the OpenRouter secret and cannot run live sweeps. It builds the tool and verifies it offline (fake client, dry-run, fixture-driven report generation). The user starts the real sweep; the key comes from `.env` via `just`. Never read, print or log the key.

**Blocked by:** 08, 09

**Status:** code done, waiting for the user-run first sweep

- [x] Stage 1: one cheap pass over all candidates (optionally capped by max price or max model count)
- [x] Stage 2: 3-5 repeats on the top N survivors (N configurable, default 10-20)
- [x] Hard spend cap: stops launching new runs when the cap is reached and says so in the report
- [x] Bounded concurrency; results are saved incrementally so an interrupted sweep can resume without re-paying for finished models
- [x] Dry-run mode prints the planned runs and estimated cost without calling any model
- [x] Markdown report: per-model table (pass rate, cost per pass, failure kinds, serving provider), list of models above the pass bar (95 %, `--min-pass-rate`), recommended model, total actual spend
- [x] Report written to a gitignored output directory; README section explains how to run it, what it costs, and that the key is taken from `.env`
- [x] Report generation is a separate step that works from saved result files, so the report can be regenerated or tested without any API call
- [x] Agent-verifiable: tests with a fake client cover both stages, spend cap, resume and report output from fixture results; dry-run works with no key
- [ ] User-run (not agent-verifiable): first real `just sweep` with a small cap; user records spend and the report in Comments

## Comments

- `just sweep` (`run` command), `just sweep --dry-run` (no key; prints planned runs and estimate), `just sweep-report` (from saved files). Output in `backend/sweep-output/` (gitignored); docs in `backend/tests/RecipeJoe.Sweep/README.md`.
- Stage 2 pins the provider that served stage 1 when it was a single one. Recommendation = cheapest model with pass rate >= 95 % (Pareto frontier dropped on request) among the models with the most runs (so a lucky one-run model doesn't beat a repeated one).
- Cap is checked before each run is launched; runs in flight finish, so the spend can overshoot slightly.
- **Still open (user-run):** first real `just sweep --cap 2 --max-output-price 1 --max-models 20` (try `--dry-run` first); record spend and report here.
