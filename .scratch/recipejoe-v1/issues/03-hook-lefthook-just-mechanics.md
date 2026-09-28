# Hook, lefthook and just mechanics

Type: research
Status: resolved
Blocked by: —

## Question

Exactly how do the three local automation layers work together for a .NET + TS monorepo?
- Claude Code PostToolUse hook: reading the edited file path from hook stdin JSON, dispatching per extension (`dotnet format --include`, `prettier --write`, `eslint --fix`), speed (is `dotnet format` per-file fast enough?), failure behaviour.
- lefthook pre-commit: staged-file globs per sub-project, `stage_fixed`, install flow.
- `just`: recipe layout for test selection (`test-unit`, `test-int`, …), passing args, use from GitHub Actions.
Deliver a concrete recommended config sketch for each.

Research: [`research/hook-lefthook-just-mechanics.md`](../../../research/hook-lefthook-just-mechanics.md) (no branch: repo guardrail blocks agent git writes). It contains config sketches for settings.json, lefthook.yml and the justfile.

## Answer

- Claude PostToolUse hook: read `tool_input.file_path` (always absolute) and find the repo root from that path, since `CLAUDE_PROJECT_DIR` doesn't follow worktrees. Exit code 2 feeds stderr back to Claude but can't undo the edit. Timeout defaults to 600s. Measured per-file C# formatting: full `dotnet format` ~3–4s, `dotnet format whitespace --folder` 0.8s, CSharpier 0.4s. `--include` silently ignores paths that aren't relative to the cwd.
- lefthook: `root:` per sub-project gives tools root-relative paths; `stage_fixed: true` re-stages fixes; run ESLint and then Prettier in sequence, not in parallel.
- just: `*args` + `"$@"` for pass-through; `extractions/setup-just@v4` in CI.
- Test-level selection: separate test projects (matches the standing decision).
- The remaining choices went to ticket 13.
