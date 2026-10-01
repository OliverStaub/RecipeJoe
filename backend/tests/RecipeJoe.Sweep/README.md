# Model sweep

Finds the best model for Video Import by running the golden cases (the recorded videos behind `just golden`) against many OpenRouter models, then ranking them by pass rate and cost. Opt-in, never part of `just test` or CI. Output goes to `backend/sweep-output/` (git-ignored).

## Commands

| Command | Spends | What it does |
|---|---|---|
| `just sweep-candidates` | nothing | Pulls the public OpenRouter model list (no key) and writes `candidates.md` and `candidates.csv`. |
| `just sweep --dry-run` | nothing | Prints the planned runs and the estimated cost. No key needed. |
| `just sweep-model <id>` | credits | One model, all golden cases: pass/fail, failure kind, serving provider, tokens, real cost. Result: `results/<id>.json`. |
| `just sweep` | credits, up to the cap | Two-stage sweep, then writes `report.md`. |
| `just sweep-report` | nothing | Rebuilds `report.md` from the saved results. |

Options go after the command, e.g. `just sweep --cap 2 --max-models 20 --max-output-price 1`.

- Filters (`candidates`, `sweep`): `--max-output-price` (USD per M tokens), `--name` (substring of id or name), `--include id,id` (always keep these, even if expiring), `--tokens-in` / `--tokens-out` (assumed tokens per golden pass, default 15500 / 6000).
- `sweep-model`: `--provider NAME` pins the OpenRouter provider (no fallback), `--repeats N`.
- `sweep`: `--max-models N` (cheapest first), `--top N` stage-2 survivors (default 10), `--repeats N` stage-2 runs each (default 3), `--cap USD` (default 5), `--concurrency N` (default 4).

## How a sweep works

1. Candidates: text in and out, `structured_outputs` supported; `:free`, `:batch`, `~` aliases, routers and models with an expiry date are dropped. Both Gemini 2.5 Flash models carry an expiry date (2026-10-20), so add them with `--include` if you want them as reference.
2. Stage 1: one golden pass per candidate, cheapest first.
3. Stage 2: the cheapest models that passed every case get more runs, pinned to the provider that served stage 1 (when it was a single one).
4. A case passes when the model finds the expected number of Recipes (`NoRecipe` where none are expected), the same bar as `just golden`.
5. Every request sends `provider.require_parameters = true`. A 402 (OpenRouter's in-flight budget) is retried with backoff; any other provider error is recorded as a failure.

Results are saved per model as they finish. Rerun `just sweep` after an interruption and finished models and runs are skipped, so nothing is paid twice. Delete `sweep-output/results/` to start over.

## Speed

The report shows wall-clock time per golden pass (median and worst over the runs). It includes retry backoff and, with `--concurrency` above 1, some queueing, so it ranks models but isn't exact latency; use `--concurrency 1` for cleaner numbers. Speed is information only: the Pareto frontier and recommendation stay on score vs cost. Results saved before timings existed show `-`.

## What it costs

The spend cap stops new runs once reached and the report says so. Runs already in flight finish, so the total can overshoot slightly. `--dry-run` shows an estimate from list prices; the report shows the real cost (`usage.cost` of each reply). Roughly: a first pass over all ~250 candidates is $10-$75 without a price cap, a few dollars with `--max-output-price 1`.

## The key

The key is read from `Llm__ApiKey`, which `just` loads from `.env` (see `.env.example`). It is never printed or logged. Use a **dedicated OpenRouter key with a spend limit** (e.g. $10) for sweeps, so a bug can't spend more than that. The recorded transcripts are public YouTube text.
