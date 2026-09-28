# Local automation choices

Type: grilling
Status: open
Blocked by: 03

## Question

Choices left by the [hook/lefthook/just research](../../../research/hook-lefthook-just-mechanics.md):
1. C# formatting in the Claude hook: whitespace-only (0.8s), full `dotnet format` (3–4s), or CSharpier (0.4s, but a second formatter)?
2. Feed unfixable ESLint/analyzer errors back to Claude via exit code 2?
3. Pre-commit `dotnet format` scope (staged files vs project) and whether to use `--no-restore`.
4. Formatting check in CI: `just fmt --check` flag or a separate `just fmt-check` recipe?
5. Install lefthook via brew, npm devDependency, or both?
6. Run Prettier on root-level files (yml, md, json)?
