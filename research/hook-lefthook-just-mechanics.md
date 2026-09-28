# Research: Claude Code hook + lefthook + just mechanics (.NET + TS monorepo)

Date: 2026-09-28. Scope: how the three autofix and task layers work for RecipeJoe (`/backend` .NET + EF Core + MSTest, `/frontend` Vite + React + TS). Already decided: `dotnet format` + `.editorconfig`, Prettier + ESLint, autofix in a Claude Code PostToolUse hook and a lefthook pre-commit hook, CI check-only, root `justfile`.

Legend: **[doc]** = stated in the primary source linked. **[src]** = read in upstream source code. **[measured]** = run locally for this note (macOS arm64, .NET SDK 10.0.401, `dotnet new webapi` + `mstest` two-project `.slnx`, warm build, just 1.58.0, CSharpier 1.3.0). **[inference]** = my conclusion, not stated anywhere.

---

## 1. Claude Code PostToolUse hook

Sources: hooks reference https://code.claude.com/docs/en/hooks (raw: `hooks.md`), hooks guide https://code.claude.com/docs/en/hooks-guide.

### Facts

- **Where it lives.** `~/.claude/settings.json` (all projects), `.claude/settings.json` (project, commit it), `.claude/settings.local.json` (project, gitignored) [doc]. Recipe-app already has `.claude/settings.json` with a PreToolUse git guard; the new block gets merged into it.
- **Shape.** event, then matcher group, then handler: `hooks.PostToolUse[].matcher` + `hooks[]` of `{type:"command", command, args?, timeout?, statusMessage?, async?}` [doc].
- **When it fires.** "After a tool call succeeds." The matcher filters on `tool_name`. `"Edit|Write"` is the documented file-edit matcher [doc]. The reference doesn't mention `MultiEdit`. Edits done through `Bash` (sed, codegen, `dotnet ef migrations add`) do **not** fire an `Edit|Write` hook. `FileChanged` exists for "whatever wrote it" [doc].
- **stdin JSON** (PostToolUse, Write example): `session_id`, `transcript_path`, `cwd`, `permission_mode`, `hook_event_name`, `tool_name`, `tool_input.file_path`, `tool_input.content`, `tool_response.filePath`, `tool_response.type`, `tool_use_id`, `duration_ms` [doc]. "File-tool `tool_input` paths arrive … always absolute" [doc]. So you read it with `jq -r '.tool_input.file_path'`.
- **Exit codes (PostToolUse).**
  - `0` = success. Stdout goes to the debug log unless it's JSON. Stderr on exit 0 is never shown to Claude [doc].
  - `2` = can't block (the tool already ran). **"Shows stderr to Claude"** [doc]. This is how you feed unfixable lint errors back to Claude.
  - Any other code = non-blocking error. The transcript shows a `hook error` notice with the first stderr line. Claude carries on [doc].
  - JSON alternative on exit 0: top-level `decision:"block"` + `reason` "adds the reason next to the tool result", or `hookSpecificOutput.additionalContext` [doc].
- **Timeout.** In seconds. Default 600 for `command` hooks [doc]. On timeout the hook is cancelled and its output discarded [doc].
- **Concurrency.** "All matching hooks run in parallel". PostToolUse "fires concurrently when Claude makes parallel tool calls" [doc]. So two edits to different `.cs` files can run two formatters at once.
- **Exec vs shell form.** If `args` is set, `command` is spawned directly with no shell. Docs: "Set `args` whenever the hook references a path placeholder". `${CLAUDE_PROJECT_DIR}` is exported as an env var either way [doc].
- **Worktree caveat.** `${CLAUDE_PROJECT_DIR}` "stays put" at the session's start root, while stdin `cwd` follows Claude into a worktree [doc]. **[inference]** The script should work out the repo root from the *edited file's* path (`git -C "$(dirname "$file")" rev-parse --show-toplevel`), not from `CLAUDE_PROJECT_DIR`. Otherwise worktree edits get formatted with the main checkout's config and paths.
- **`if` field.** Takes one permission rule per handler, e.g. `"Edit(*.ts)"` [doc]. You could dispatch by extension with no script, but Edit and Write × 3 extension groups gives 6 handlers. One script is simpler [inference].
- The official Prettier example is `jq -r '.tool_input.file_path' | xargs npx prettier --write` [doc, hooks-guide].
- Not documented: whether Claude's file-state tracking shows a warning after a hook rewrites the file. Untested here.

### dotnet format speed (the key question)

`dotnet format` "may restore, compile, and run analyzers" and loads the project or solution through MSBuild. `whitespace --folder` treats the argument as "a simple folder of code files" [doc, https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format]. The subcommands are `whitespace`, `style` and `analyzers`. The top-level `--include` takes a space-separated list of relative paths [doc].

Wall-clock time to format one file, on a tiny 2-project solution [measured]:

| Command | Time | Fixed file? |
|---|---|---|
| `dotnet format X.slnx --include f.cs` (implicit restore) | 4.3 s | yes |
| same + `--no-restore` | 3.1 s | yes |
| `dotnet format Api/Api.csproj --include f.cs --no-restore` | 2.9 s | yes |
| `dotnet format whitespace X.slnx --include f.cs --no-restore` | 1.7 s | yes |
| `dotnet format whitespace <dir> --folder --include f.cs` | **0.8 s** | yes |
| `dotnet csharpier format <abs path>` | **0.4–0.5 s** | yes |

Also measured:

- `--folder` mode honours `.editorconfig`. `indent_size = 2` was applied.
- **Path gotcha.** In solution/project mode, `--include` is resolved **relative to the current working directory**. An absolute path, or a path relative to the `.sln` directory from a different cwd, is **silently ignored**: exit 0 and the file is unchanged. In `--folder` mode the path is relative to the folder argument, and an absolute path worked. **The hook must `cd` and pass a relative path.**
- `--verify-no-changes` exits **2** when changes are needed (whitespace --folder).
- A real solution with EF Core, analyzers and several projects will be slower than this tiny one. Load time grows with project count [inference]. That makes full `dotnet format` (3 s or more) noticeable on every edit, since the hook blocks Claude.

**CSharpier as an alternative:** `dotnet csharpier format <file>` and `check` (exit 1 if unformatted) [doc https://csharpier.com/docs/CLI, measured]. It's opinionated: it reads only `printWidth`, `useTabs`, `indentSize` and `endOfLine` (from `.csharpierrc` or `.editorconfig`) [doc https://csharpier.com/docs/Configuration]. It conflicts with `dotnet format whitespace` and IDE0055 unless you disable IDE0055 and use only `dotnet format style`/`analyzers` [doc https://csharpier.com/docs/IntegratingWithLinters]. **This would change the already-decided formatter, so it's the user's call.**

### Prettier / ESLint facts

- Prettier: `--write`, `--check` (exit 1 if unformatted, 2 on error), `--ignore-unknown`, `--cache` [doc https://prettier.io/docs/cli].
- ESLint: `--fix` writes fixes and reports what's left. Exit 0 = clean, 1 = lint errors remain, 2 = config/internal error. `--no-warn-ignored` silences warnings for explicitly passed ignored files. `--cache` [doc https://eslint.org/docs/latest/use/command-line-interface].
- Order: `eslint --fix` then `prettier --write`, so Prettier wins on layout. This assumes `eslint-config-prettier` turns off conflicting ESLint rules [inference, standard Prettier guidance].

### Recommended sketch

`.claude/settings.json` (merge into the existing `hooks` object):

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "${CLAUDE_PROJECT_DIR}/.claude/hooks/format-file.sh",
            "args": [],
            "timeout": 60,
            "statusMessage": "Formatting edited file"
          }
        ]
      }
    ]
  }
}
```

`.claude/hooks/format-file.sh` (needs `jq`, which is already a dependency of the existing guard hook):

```bash
#!/usr/bin/env bash
# PostToolUse: format the single file Claude just edited.
# exit 0 = fine/ignored; exit 2 = show stderr to Claude (unfixable lint errors).
set -uo pipefail
file=$(jq -r '.tool_input.file_path // empty')
[[ -n "$file" && -f "$file" ]] || exit 0
root=$(git -C "$(dirname "$file")" rev-parse --show-toplevel 2>/dev/null) || exit 0
rel=${file#"$root"/}

case "$rel" in
  backend/*.cs)
    cd "$root/backend" || exit 0
    # whitespace-only, no project load (~0.8s). Style/analyzer fixes happen in pre-commit.
    dotnet format whitespace . --folder --include "${rel#backend/}" >/dev/null 2>&1 || exit 0
    ;;
  frontend/*.ts|frontend/*.tsx|frontend/*.js|frontend/*.jsx|frontend/*.mjs|frontend/*.cjs)
    cd "$root/frontend" || exit 0
    f=${rel#frontend/}
    out=$(npx --no-install eslint --fix --no-warn-ignored "$f" 2>&1); rc=$?
    npx --no-install prettier --write --ignore-unknown --log-level warn "$f" >/dev/null 2>&1
    if [[ $rc -eq 1 ]]; then printf 'ESLint errors in %s:\n%s\n' "$rel" "$out" >&2; exit 2; fi
    ;;
  frontend/*)
    cd "$root/frontend" || exit 0
    npx --no-install prettier --write --ignore-unknown --log-level warn "${rel#frontend/}" >/dev/null 2>&1
    ;;
esac
exit 0
```

Design notes:

- Formatter failures, a missing `node_modules` or a missing SDK all exit 0 and are silent. A missing tool must never interrupt Claude. CI and pre-commit are the backstop.
- In bash `case`, `*` matches `/`, so `backend/*.cs` covers nested files.

---

## 2. lefthook (pre-commit, staged files, re-stage)

Sources: https://lefthook.dev/configuration/ (stage_fixed, root, glob, glob_matcher, jobs, parallel, run, fail_on_changes), https://lefthook.dev/installation/, source at github.com/evilmartians/lefthook (`internal/run/controller/filter/filter.go`, `job.go`, `internal/templates/hook.tmpl`). Latest is v2.1.14 (2026-09-14).

### Facts

- **`{staged_files}`** is replaced with the staged files after filters. Long lists are split into several sequential runs to stay under the argv limit [doc].
- **`glob`** is matched from the **repo root** and ignores `root` [doc]. It can be a list (≥1.10.10) [doc]. The default matcher is gobwas: `*` matches across `/` (compiled without separators) [src], and `**/*.js` does *not* match top-level `app.js` [doc]. `glob_matcher: doublestar` gives bash-like `**` [doc].
- **`root`** sets the job's cwd and filters files to that prefix [doc]. The source applies filters in the order glob, exclude, root, file_types. `byRoot` rewrites `frontend/src/a.ts` to `./src/a.ts` [src]. So paths reach the tool **relative to `root`**, which is what `dotnet format --include` needs. The "filters" example page still shows repo-relative output; the source wins.
- **`stage_fixed: true`** (pre-commit only) runs `git add` on the job's filtered files after it runs. With `root`, paths are re-joined to the root [src]. "If the git add call fails, the hook fails too" [doc].
- **Partially staged files.** Since 1.3.0 lefthook hides unstaged changes during pre-commit and restores them afterwards. Restoring was hardened in #1416/#1417 [changelog]. So `stage_fixed` doesn't sweep unstaged hunks into the commit [inference from changelog].
- **`parallel: true`** runs jobs concurrently. The default is sequential [doc]. `jobs` (≥1.10.0) supports `group:` with its own `piped`/`parallel`. Groups pass `root`, `glob`, `exclude`, `env` and `files` down to nested jobs; everything else, including `stage_fixed`, must be set per job [doc].
- **`fail_on_changes`**: `never` (default), `always`, `ci` or `non-ci` [doc]. Keep the default. Lefthook doesn't run in CI here anyway.
- **Install.**
  - `brew install lefthook` [doc].
  - `npm install --save-dev lefthook`: the postinstall runs `lefthook install` [doc].
  - `go install github.com/evilmartians/lefthook/v2@v2.1.14` [doc].
  - `lefthook install` writes the git hooks. You don't need to re-install after editing `lefthook.yml` [doc]. `LEFTHOOK=0 git commit` skips hooks [doc].
  - The generated hook finds the binary via `$LEFTHOOK_BIN`, then the install-time path, then `lefthook` on PATH, then `<repo>/node_modules/...`, then **`<repo>/<job root>/node_modules/...`** [src]. So an npm devDependency in `frontend/` is found once a job has `root: frontend/`.

### Recommended sketch (`lefthook.yml`, repo root)

```yaml
min_version: 2.1.0
glob_matcher: doublestar

pre-commit:
  parallel: true            # backend and frontend groups run side by side
  jobs:
    - name: backend
      root: backend/
      glob: "backend/**/*.cs"
      group:
        jobs:
          - name: dotnet-format
            # full whitespace+style+analyzers (severity warn); loads solution (~3s+).
            # paths arrive relative to backend/, which is what --include needs.
            run: dotnet format RecipeJoe.slnx --include {staged_files}
            stage_fixed: true

    - name: frontend
      root: frontend/
      group:
        piped: true         # eslint then prettier on the same files; never concurrently
        jobs:
          - name: eslint
            glob: "frontend/**/*.{ts,tsx,js,jsx,mjs,cjs}"
            run: npx --no-install eslint --fix --no-warn-ignored {staged_files}
            stage_fixed: true
          - name: prettier
            glob: "frontend/**/*.{ts,tsx,js,jsx,mjs,cjs,json,css,scss,html,md,yml,yaml}"
            run: npx --no-install prettier --write --ignore-unknown {staged_files}
            stage_fixed: true
```

Notes:

- ESLint exit 1 (unfixable errors) fails the commit. That's intended.
- Change `RecipeJoe.slnx` to match the real solution file name.
- `.editorconfig`, `*.csproj` and root `*.md` aren't covered here. Add a root-level Prettier job if you want Markdown/YAML outside `frontend/` formatted too. That needs Prettier resolvable from the root.
- **Install recommendation:** a `just setup` recipe that runs `lefthook install`. Document `brew install lefthook just` for macOS. Optionally also add `lefthook` as a `frontend` devDependency so `npm ci` auto-installs the hooks.

---

## 3. just (task runner, local + CI)

Sources: just manual https://just.systems/man/en/ (recipe-parameters, positional-arguments, working-directory, private-recipes, the-default-recipe, github-actions, pre-built-binaries), setup-just README (`extractions/setup-just@v4`).

### Facts

- **Variadic params.** `*args` takes zero or more, `+args` one or more. `{{args}}` interpolates them space-joined, so quoting is lost [doc]. With `set positional-arguments`, `"$@"` forwards the args with their quoting intact [doc].
- **Flags after the recipe name reach the recipe.** `just test-unit --filter "X~Y Z"` passed through; `just test -v n` passed through to a dependency [measured].
- **Dependency with args:** `test *args: (test-unit args)` [doc, measured].
- **Working directory.** Recipes run in the justfile's directory by default. `[working-directory: 'backend']` changes it per recipe (≥1.38) [doc, measured].
- **Default recipe.** `[default]` attribute or the first recipe. `set default-list := true` (≥1.52) lists recipes instead [doc]. `_name` or `[private]` hides a recipe from `--list` [doc].
- **GitHub Actions.**
  - `- uses: extractions/setup-just@v4` (optional `just-version`, uses `github.token` to avoid rate limits) [doc, setup-just README v4].
  - Or `taiki-e/install-action@just` [doc].
  - `install.sh` can hit GitHub API rate limits in CI; pin `--tag` [doc].

### MSTest filter gotcha

If `test-unit` hard-codes `--filter TestCategory=Unit` and the user passes `--filter …`, the command gets two `--filter` options [measured: both passed through]. [inference] Which one wins depends on the test runner. Split test projects by level (`*.UnitTests`, `*.IntegrationTests`, `*.ContractTests`), so selection is by project and `*args` forwards freely. Or accept a separate `filter=""` parameter. **User decision.**

### Recommended sketch (`justfile`, repo root)

```just
set positional-arguments
set default-list := true

sln := "RecipeJoe.slnx"

# --- tests -----------------------------------------------------------------
# all levels; extra args go to every dotnet test run
test *args: (test-unit args) (test-int args) (test-contract args) (test-e2e)

[working-directory: 'backend']
test-unit *args:
  dotnet test tests/RecipeJoe.UnitTests "$@"

[working-directory: 'backend']
test-int *args: up
  dotnet test tests/RecipeJoe.IntegrationTests "$@"

[working-directory: 'backend']
test-contract *args:
  dotnet test tests/RecipeJoe.ContractTests "$@"

[working-directory: 'frontend']
test-e2e *args:
  npx playwright test "$@"

cov *args:
  cd backend && dotnet test {{sln}} --collect:"XPlat Code Coverage" "$@"
  cd frontend && npx vitest run --coverage

mutate *args:
  cd backend && dotnet stryker "$@"

# --- format / lint ---------------------------------------------------------
# local: fix. CI: `just fmt --check` (check-only)
fmt mode="fix":
  #!/usr/bin/env bash
  set -euo pipefail
  if [[ "{{mode}}" == "--check" ]]; then
    (cd backend && dotnet format {{sln}} --verify-no-changes)
    (cd frontend && npx prettier --check .)
  else
    (cd backend && dotnet format {{sln}})
    (cd frontend && npx prettier --write .)
  fi

lint:
  cd frontend && npx eslint . --max-warnings 0
  cd frontend && npx tsc --noEmit
  cd backend && dotnet build {{sln}} -warnaserror --no-incremental

# --- env -------------------------------------------------------------------
up:
  docker compose up -d --wait

down:
  docker compose down

setup:
  lefthook install
```

Notes:

- The recipes that use `"$@"` rely on `set positional-arguments`.
- The `vitest run` step in `cov` assumes Vitest unit tests sit alongside the .NET ones; adjust to taste.
- `mutate` assumes Stryker.NET as a local dotnet tool, which is a separate ticket.
- In `lint`, `-warnaserror` makes analyzers gate CI.
- The `#!/usr/bin/env bash` recipe is a shebang recipe. It needs bash on the CI runner (present on ubuntu-latest).

CI usage:

```yaml
- uses: actions/checkout@v4
- uses: extractions/setup-just@v4
  with: { just-version: '1.58' }
- uses: actions/setup-dotnet@v4
- uses: actions/setup-node@v4
- run: npm ci
  working-directory: frontend
- run: just fmt --check
- run: just lint
- run: just test-unit
```

---

## Decisions for the user

1. **What the hook does for C#.**
   - (a) `dotnet format whitespace --folder`: about 0.8 s, whitespace only. Recommended.
   - (b) full `dotnet format --include`: about 3 s or more per edit, plus style/analyzer fixes.
   - (c) switch to CSharpier: about 0.4 s, but it reverses the "dotnet format" decision and IDE0055 must be disabled.
2. **Hook feedback on unfixable ESLint errors.** Exit 2 feeds the errors back to Claude (recommended). The alternative is silent, leaving them to pre-commit. Exit 2 can be noisy mid-refactor.
3. **Pre-commit `dotnet format` scope.**
   - Full (whitespace+style+analyzers, a few seconds per commit that touches C#; recommended).
   - Or `whitespace` only, which is fast but leaves style fixes to a CI failure.
   - `--no-restore` saves about 1 s but fails on a fresh clone.
4. **How to select test levels.** Separate test projects (recommended) or `TestCategory` filters with a `filter` parameter.
5. **Check-mode interface.** `just fmt --check` or a separate `fmt-check` recipe. `lint` could also run the format verification.
6. **lefthook install channel.** brew + `just setup` (recommended), npm devDependency (automatic via postinstall), or both.
7. **Scope beyond `backend/` and `frontend/`.** Should root Markdown/YAML (e.g. `docker-compose.yml`, `.github/**`) be Prettier-formatted? That needs Prettier at the root.
