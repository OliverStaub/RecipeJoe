# Local automation choices

Type: grilling
Status: resolved
Blocked by: 03

## Question

Choices left by the [hook/lefthook/just research](../../../research/hook-lefthook-just-mechanics.md):
1. C# formatting in the Claude hook: whitespace-only (0.8s), full `dotnet format` (3–4s), or CSharpier (0.4s, but a second formatter)?
2. Feed unfixable ESLint/analyzer errors back to Claude via exit code 2?
3. Pre-commit `dotnet format` scope (staged files vs project) and whether to use `--no-restore`.
4. Formatting check in CI: `just fmt --check` flag or a separate `just fmt-check` recipe?
5. Install lefthook via brew, npm devDependency, or both?
6. Run Prettier on root-level files (yml, md, json)?

## Answer

1. **Hook, C#**: `dotnet format whitespace --folder` only (~0.8s). Style/analyzer fixes left to pre-commit + CI.
2. **Hook, ESLint**: unfixable errors (eslint exit 1) → hook exits 2, stderr fed back to Claude. Formatter/tool failures stay silent (exit 0).
3. **Pre-commit, C#**: full `dotnet format` (whitespace + style + analyzers) on staged files via `--include`; no `--no-restore` (fresh clones must work).
4. **CI format check**: separate `just fmt-check` recipe (not `fmt --check`, not folded into `lint`).
5. **lefthook install**: `brew install lefthook just` + `just setup` (runs `lefthook install`; later also `npm ci` / `dotnet restore`). `min_version: 2.1.0` in `lefthook.yml` guards the version. No npm devDependency.
6. **Root-level Prettier**: YAML + JSON at the repo root (`docker-compose.yml`, `.github/**`, `*.json`) formatted; Markdown **not** formatted. `.prettierrc` lives at the repo root (frontend finds it by upward search). The root lefthook job and `fmt`/`fmt-check` call `frontend/node_modules/.bin/prettier`; no root `package.json`.

Config sketches: [research note](../../../research/hook-lefthook-just-mechanics.md), adjusted by the answers above (whitespace hook as sketched; add `fmt-check` + a root yml/json Prettier job; drop the `fmt mode` parameter).
