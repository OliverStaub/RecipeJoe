# 06: Local hooks (lefthook + Claude PostToolUse)

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §4.1; decision: [Local automation choices](../../recipejoe-v1/issues/13-local-automation-choices.md)

**What to build:** Formatting and lint are autofixed on every edit (Claude hook) and on every commit (lefthook).

**Blocked by:** 05 (justfile)

**Status:** resolved

- [ ] Claude PostToolUse hook in `.claude/settings.json`, keeping the existing git guardrail
  - reads `tool_input.file_path` and finds the repo root from it
  - `.cs` → `dotnet format whitespace --folder --include`
  - TS/JS → `eslint --fix` + `prettier --write`
  - unfixable ESLint errors → exit 2 with stderr; tool failures → exit 0
- [ ] The hook script has a test script, like the existing guardrail hook
- [ ] `lefthook.yml` (`min_version: 2.1.0`), a `root:` per sub-project, `stage_fixed: true`
  - backend: full `dotnet format --include <staged>`
  - frontend/e2e: ESLint, then Prettier
  - root YAML/JSON: Prettier
  - no tests, no pre-push
- [ ] `just setup` runs `lefthook install`
- [ ] Committing a badly formatted file produces a formatted, re-staged commit
