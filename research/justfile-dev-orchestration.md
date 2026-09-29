# Research: justfile structure and `just dev` multi-process orchestration

Date: 2026-09-29. Scope: is the root `justfile` a sound way to structure tasks, especially `just dev`? That recipe is a shebang recipe that runs `docker compose up -d --wait postgres`, backgrounds Vite with `(cd frontend && exec vite) &`, cleans up with `trap … EXIT` and runs `dotnet watch` in the foreground. This note also covers how multi-process dev setups are usually built.

Legend: **[doc]** = stated in the primary source linked. **[src]** = read in upstream source code. **[measured]** = run locally for this note in `/private/tmp/claude-503/` (macOS arm64, just 1.58.0, Docker Compose 5.5.1, Node 26.9.0, Vite 8.3.1 from `frontend/node_modules`, `/usr/bin/env bash` = GNU bash 3.2.57). **[inference]** = my conclusion, not stated anywhere.

---

## 1. just: recipe kinds, settings, attributes, signals

Sources: manual https://just.systems/man/en/ (settings, shebang-recipes, attributes), README https://github.com/casey/just/blob/master/README.md (sections Shell, Positional Arguments, Dotenv Settings, Parallelism, Groups, Signal Handling), `Cargo.toml` in the same repo.

### Facts

- **Linewise vs shebang.**
  - In a linewise recipe, each line runs as its own `set shell` invocation.
  - Shebang recipes are "executed by saving the recipe body to a file in a temporary directory, marking the file as executable, and executing it" [doc].
- **`set shell` does not apply to shebang recipes.** The README says the `shell` setting "controls the command used to invoke recipe lines and backticks. Shebang recipes are unaffected" [doc].
  - Measured: a linewise recipe ran with `$-`=`ehuBc`, so `-e` and `-u` were on. The shebang recipe ran with `$-`=`hB`, and an unset variable expanded silently [measured].
  - So `just dev` needs its own `set -euo pipefail`, which it has.
- **`[script]`** (≥1.33) runs the recipe with `set script-interpreter` (default `sh -eu`), "*not* the value of `set shell`" [doc]. It's an alternative to a shebang line; it has no advantage here [inference].
- **`set positional-arguments`.** Arguments are passed as `$1`…, and `"$@"` keeps their quoting [doc]. This works in shebang recipes too: `just shebang "a b" c` gave `$#`=2 and `$1`=`a b` [measured]. In shebang recipes `$0` is the temp script path, e.g. `/var/folders/…/just-XXXX/shebang` [measured]. In shell recipes `$0` is the recipe name [doc].
- **`set dotenv-load`** loads `.env` if present [doc]. The manual doesn't say whether `.env` values are interpolated.
  - just pins `dotenvy = "0.15.0"` [src].
  - In a `.env` containing `B=${A}-world` and `C=$A/x`, just expanded both, giving `hello-world` and `hello/x` [measured]. **So `$` in `.env` values (e.g. a password) gets substituted.**
  - Newer dotenvy (main) makes substitution opt-in [doc, dotenvy README]. That doesn't affect just 1.58.
- **`[group('name')]`** (≥1.27) groups recipes and modules under headings in `--list` [doc]. A recipe can be in several groups [doc].
  - Measured output: the ungrouped recipes are listed first, then `[dev]` and `[test]` blocks [measured].
  - `--list --unsorted` "prints recipes in their justfile order within each group" [doc]. So `[group]` works with the existing `list` default recipe.
- **`[parallel]`** (≥1.42): "Run this recipe's dependencies in parallel" [doc]. `--jobs` (≥1.56) limits concurrency [doc]. In the parallel tests I measured:
  - Two 1 s deps took 1.03 s in total.
  - Output is interleaved with **no per-process prefix**.
  - When one dep failed with exit 3 after 1 s, just **kept waiting** for the 5 s sibling, then exited 3. Nothing is killed when a sibling dies.
  - SIGINT to the process group killed all deps (they're in the foreground group) and just exited.
  - It only parallelises *dependencies*, and has no readiness ordering between them [doc].
  - **[inference]** Fine for bounded jobs (lint/test fan-out). It's weak for long-running dev servers: there's no "one dies, stop all", no log prefixes, and no ready-wait. The Postgres `--wait` would still have to be a normal dependency run first.
- **`[working-directory('dir')]`** (≥1.38) sets the recipe's cwd. The path may be an expression [doc].
- **Signals** [doc, README "Signal Handling"]:
  - "SIGHUP, SIGINT, and SIGQUIT … are sent to all processes in the foreground process group". If no child is running, just exits immediately. If one is, just "will wait until it terminates, to avoid leaving it behind".
  - SIGTERM is forwarded to children (≥1.41).
  - After a fatal signal "just halts execution" even if the child succeeded, unless the recipe has `[continue]` (≥1.54).
  - So Ctrl-C reaches bash, Vite and `dotnet watch` directly. just only waits.

---

## 2. bash: background jobs, `exec`, traps; Node's signal reset

Sources: GNU Bash Reference Manual https://www.gnu.org/software/bash/manual/html_node/Signals.html, …/Lists.html, …/Bourne-Shell-Builtins.html. The texts were quoted from the same manual's source, https://git.savannah.gnu.org/cgit/bash.git/plain/doc/bashref.texi, because gnu.org returned 403/429. Node.js https://nodejs.org/api/process.html#signal-events, source `src/node.cc`. Vite source `dist/node/chunks/node.js` (8.3.1).

### Facts

- **Background jobs and SIGINT/SIGQUIT.** "When job control is not in effect, asynchronous commands ignore SIGINT and SIGQUIT in addition to these inherited handlers" [doc].
  - Shebang recipes run as a non-interactive shell, and "Bash does not enable job control by default when the shell is not interactive" [doc].
- **Background jobs and stdin.** "If a command is followed by a `&` and job control is not active, the default standard input for the command is the empty file /dev/null" [doc]. The backgrounded process's fd 0 was `/dev/null` [measured].
- **`exec` in the subshell.** `exec` "replaces the shell without creating a new process" [doc].
  - Measured on bash 3.2:
    - `(cd d && exec sleep 7) &` gives a `$!` whose command is `sleep`.
    - Without `exec`, `$!` is a `bash` subshell. `kill $!` then killed only the subshell, and **`sleep` survived as an orphan**.
  - So the `exec` in `just dev` is load-bearing.
- **SIGINT to a foreground command.** When job control is off, bash "waits until that foreground command terminates". If the command died from SIGINT, bash acts on it (a non-interactive shell exits). If the command handled SIGINT and exited normally, bash carries on [doc].
- **`trap … EXIT`.** "If a sigspec is 0 or EXIT, action is executed when the shell exits" [doc]. The trap is not run while a foreground command is still running; bash waits for it to finish first [doc].
  - Test: just shebang recipe with a background Vite and a foreground Node process, SIGINT sent to the whole process group. The EXIT trap fired, no Vite was left, and just reported `recipe dev was terminated by signal 2` [measured].
  - Either exit path of `dotnet watch` ends the script, so the trap runs both ways [inference].
- **Signals ignored on entry.** "signals ignored upon entry to a non-interactive shell cannot be trapped or reset" [doc]. This applies to bash itself, not to its children.
- **Node resets inherited signal dispositions.** `ResetSignalHandlers()`: "Restore signal dispositions, the parent process may have changed them". It sets every signal except SIGKILL, SIGSTOP, SIGPIPE and SIGXFSZ back to `SIG_DFL` [src]. Node's docs only say SIGINT/SIGTERM have default handlers that exit with 128+signal [doc].
  - `sleep 30 &` survived `kill -INT` (still ignored), but `node … &` **died** on SIGINT [measured]. Backgrounded real Vite also died on SIGINT [measured].
- **Vite also listens for SIGTERM and stdin `end`.** `setupSIGTERMListener` registers `process.once("SIGTERM")` and, unless `CI=true`, `process.stdin.on("end")` [src]. With stdin `/dev/null`, Vite was still alive after 3 s. `'end'` doesn't fire unless stdin is read [measured]. `kill $web` (SIGTERM) is a clean shutdown.
- **Result.** The comment in `just dev` says "background jobs ignore SIGINT, so only [the API] gets Ctrl-C". That's **true for bash's disposition but not for Vite**. Node undoes the ignore, so Ctrl-C kills Vite directly and the trap's `kill` usually finds it already exiting.
  - Test: at trap time Vite was still alive, because shutdown is async [measured].
  - The pattern is still correct either way: signal or trap, Vite goes away [inference].
  - The comment could say "the trap stops whatever is left".
- **Bash version.** On this Mac, `#!/usr/bin/env bash` resolves to bash 3.2.57 (no Homebrew bash installed) [measured]. Nothing in `just dev` needs bash 4+ [inference].

---

## 3. Docker Compose: volumes, project names, merging ports

Sources: https://docs.docker.com/reference/cli/docker/compose/down/, https://docs.docker.com/compose/how-tos/project-name/, https://docs.docker.com/reference/compose-file/merge/ (source `docker/docs` `content/reference/compose-file/merge.md`), compose-go PRs https://github.com/compose-spec/compose-go/pull/380 and /pull/552.

### Facts

- **The postgres image declares `VOLUME`.** `postgres:17-alpine` has `Config.Volumes` = `{"/var/lib/postgresql/data":{}}` [measured]. Without a `volumes:` entry, every new container gets an **anonymous** volume.
- **`down` without `-v`.** "Anonymous volumes are not removed by default. However, as they don't have a stable name, they are not automatically mounted by a subsequent `up`" [doc]. `-v` removes named volumes declared in `volumes:` and anonymous volumes attached to containers [doc].
  - Measured with a marker file in PGDATA:
    - `up --force-recreate` keeps the data (Compose re-attaches the old anonymous volume).
    - `down` then `up` gives **empty data**, and the old volume is left dangling.
  - So with anonymous volumes, `just down` (no `-v`) silently resets the DB and leaks a volume per cycle. **A named volume (`pgdata:`) fixes this**, and `just down -v` then means "delete data" as documented [inference].
- **Project name** "isolate[s] environments from each other" [doc]. Precedence, highest first [doc]:
  1. `-p`
  2. `COMPOSE_PROJECT_NAME`
  3. top-level `name:`
  4. the project directory's basename
  5. the cwd basename
  - Allowed characters: lowercase letters, digits, `-` and `_`, starting with a letter or digit [doc].
  - Named volumes are scoped by project: with `-p recipejoe-e2e`, `pgdata` resolves to `recipejoe-e2e_pgdata` [measured].
- **Today's hazard.** `e2e_compose` has no `-p`, so it shares the default project (`recipe-app`) with `just dev`'s postgres. `just test-e2e` runs `down -v` first, which deletes that database. With a named volume, it deletes the named dev volume [inference from the `down` docs].
- **Worktree hazard.** The default project name is the directory basename, so a git worktree becomes a *different* project, with a separate volume and clashing host ports. A top-level `name: recipejoe` pins it [inference from the precedence list].
- **Merging `ports`.** Sequences are normally appended. `ports` is a unique resource keyed by `{ip, target, published, protocol}` [doc].
  - `8080:80` in the base plus `8090:80` in an override gave **both** ports [measured]. A plain override cannot move a port.
- **`!reset` / `!override`** [doc, merge page]:
  - `!reset` removes an attribute. The docs suggest `!reset []` / `!reset null`.
  - `!override` "allows you to fully replace an attribute, bypassing the standard merge rules". The docs' own example uses `ports: !override`.
  - Measured with the repo's current `compose.yaml` + `compose.e2e.yaml` (`-p recipejoe-e2e --profile app --profile fixtures config`): postgres, backend and fixtures published `[]`, and web only `127.0.0.1:8090:80` [measured]. This confirms the plan.
  - **Version.** The merge docs name no minimum version. The loader support landed in compose-go #380 (merged 2023-04-25, `!reset`) and #552 (merged 2024-01-29, `!override`) [src]. That maps to roughly Compose v2.18 / v2.24, but I could not confirm the exact Compose release from release notes. It's irrelevant for 5.5.1 [inference].
- **Env-var alternative.** `web` already publishes `${WEB_PORT:-8080}`, so `WEB_PORT=8090` would move it without `!override`. The postgres and backend ports would still clash with `just dev` (5432, 5080), so `!reset` is still needed for those [inference].

---

## 4. Common alternatives for multi-process dev

Sources: overmind https://github.com/DarthSim/overmind (v2.5.1, 2024-03), hivemind https://github.com/DarthSim/hivemind, foreman https://github.com/ddollar/foreman, concurrently https://github.com/open-cli-tools/concurrently (v10.0.5, 2026-08), mprocs https://github.com/pvolok/mprocs (now redirects to `pvolok/dekit`), Aspire https://aspire.dev/get-started/app-host/, https://aspire.dev/integrations/frameworks/javascript/, https://aspire.dev/integrations/databases/postgres/postgres-host/.

### Facts

- **Procfile runners.**
  - foreman "started it all" [doc, hivemind README]. hivemind says such tools make processes think they log to a file, "severe lagging, losing or breaking colored output". Hivemind uses a pty to fix this [doc].
  - overmind runs processes in tmux. Features [doc]:
    - `overmind connect <proc>` for input
    - restarting one process
    - `OVERMIND_AUTO_RESTART`
    - "when a process dies, Overmind will interrupt all other processes" unless `-c`/`--any-can-die`
  - overmind needs tmux [doc].
- **concurrently** (npm): prefixed output, `--kill-others` "all commands are killed if one dies", and `successCondition` of `first`/`last`/all [doc].
- **mprocs → dekit.** The repo now describes itself as dekit, "the next version of mprocs". It adds a TUI, task `deps`, `ready: { log: … }` checks and restarts, and `dekit mprocs` runs an existing `mprocs.yaml` [doc].
- **.NET Aspire AppHost** [doc]:
  - A code-first AppHost declares resources. Running it starts a dashboard and containers, and starts services in dependency order. It needs a container runtime.
  - Postgres: `AddPostgres("postgres").WithDataVolume()`, `.WithLifetime(ContainerLifetime.Persistent)`, `.AddDatabase("db")`.
  - The API: `AddProject<Projects.X>().WithReference(db).WaitFor(db)` injects `ConnectionStrings__db`.
  - Vite: `AddViteApp("web","./frontend")` (package `Aspire.Hosting.JavaScript`) runs the `dev` script, sets `PORT`, and uses npm by default.

### Tradeoffs [inference]

| Option | Fits when | Cost for RecipeJoe |
|---|---|---|
| just shebang + `&` + trap (current) | 2–3 processes, one is "the main one", no extra tools | Hand-written cleanup; Vite and dotnet output interleave unprefixed; no restart of one process |
| Procfile + overmind/hivemind | Many long-running processes, want per-process restart/attach | New tool (+ tmux for overmind); Postgres still via compose |
| `concurrently` | JS-centric repos already in npm | Adds a devDependency to a repo whose API is .NET; still needs compose first |
| mprocs/dekit | Want a TUI with per-process panes and readiness | New binary; young rename |
| Aspire AppHost | .NET-first team, many services, want dashboard/OTel, service discovery | Extra AppHost project + packages; replaces compose for dev (not for e2e/prod here); heavier learning curve, but on-theme for a .NET DevOps learning repo |
| `[parallel]` in just | Bounded parallel jobs (lint/test) | Not a process supervisor (see §1) |

---

## 5. Conclusion for RecipeJoe [inference]

- **The current pattern is sound and conventional for 2–3 processes.** Postgres comes from compose with `--wait` for readiness. The secondary process is backgrounded and its real PID is captured via `exec`. The primary process holds the terminal, and an EXIT trap does the cleanup. Every step is in the facts above.
- **One wording fix.** Ctrl-C does reach Vite, because Node resets the inherited SIG_IGN (§2). The trap is a safety net, not the only way Vite stops.
- **Fixes being applied, all supported by the facts:**
  - **Named postgres volume (`pgdata`).** Stops `down` → `up` wiping data and leaking volumes (§3).
  - **Separate e2e project, `-p recipejoe-e2e`.** Isolates containers and volume `recipejoe-e2e_pgdata`, so `test-e2e`'s `down -v` no longer destroys dev data (§3).
  - **Publish only web `8090` in e2e.** Use `!reset []` on postgres/backend/fixtures `ports` and `!override` on web; plain merging would append, not replace. Verified with `docker compose config` (§3). `e2e/playwright.config.ts` already defaults to `http://localhost:8090`.
  - **`[group(...)]`** (≥1.27) on recipes. Works with `--list --unsorted` (§1).
- **Optional, not in the plan:**
  - top-level `name:` in `compose.yaml` so worktrees share one project, or `-p` per worktree if isolation is wanted
  - a note that `$` in `.env` is interpolated
  - keep `[parallel]` for fan-out tasks, not `dev`
- **When to switch.** If a third long-running app process appears (worker, second frontend), or per-process restart or a dashboard becomes useful, Aspire is the most on-stack option. overmind/dekit are the lightest tool-only options.
