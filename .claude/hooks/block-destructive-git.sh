#!/usr/bin/env bash
# PreToolUse guard for the Bash tool: blocks git invocations that would
# change repository state (commit, push, merge, reset, clean, add, etc.)
# Allows read-only git commands (status, log, diff, show, branch/tag/remote
# listing, plain fetch, config --get, ...).
#
# Agent-worktree carve-out (the only mutating git allowed):
# - git worktree list|prune; add|remove only for paths under .claude/worktrees/
#   (remove without --force, so uncommitted work is never discarded)
# - git switch -c research/<name>, inside an agent worktree
# - git add / git commit, inside an agent worktree on a research/* or
#   worktree-agent-* branch
# - git branch -d/-D, only for worktree-agent-* branches
#
# This is a heuristic firewall, not a sandbox: it inspects the literal
# command string. It cannot catch every possible obfuscation (e.g. a
# base64-encoded command, or git invoked from inside a script file it
# then executes), but it blocks the direct, common ways Claude Code would
# run a mutating git command.
set -uo pipefail

input=$(cat)

# Fail closed: without jq we can't parse the command, so block anything mentioning git.
if ! command -v jq >/dev/null 2>&1; then
  case "$input" in
    *git*)
      echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"jq is not installed; the git guard fails closed"}}'
      exit 0
      ;;
  esac
  echo '{}'
  exit 0
fi

command=$(printf '%s' "$input" | jq -r '.tool_input.command // empty')
cwd=$(printf '%s' "$input" | jq -r '.cwd // empty')
[ -z "$cwd" ] && cwd=$PWD

[ -z "$command" ] && { echo '{}'; exit 0; }

deny() {
  jq -n --arg reason "$1" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$reason}}'
  exit 0
}

unquote() {
  local s=$1
  s=${s#[\"\']}
  s=${s%[\"\']}
  printf '%s' "$s"
}

# Absolute path of $1 relative to base dir $2 (no symlink resolution).
abspath() {
  local p
  p=$(unquote "$1")
  case "$p" in
    /*) ;;
    "~") p=$HOME ;;
    "~/"*) p="$HOME/${p#\~/}" ;;
    *) p="$2/$p" ;;
  esac
  printf '%s' "$p"
}

in_agent_worktree() {
  case "$1" in
    *..*) return 1 ;;
    */.claude/worktrees/?*) return 0 ;;
    *) return 1 ;;
  esac
}

is_agent_branch() {
  case "$1" in
    research/?* | worktree-agent-?*) return 0 ;;
    *) return 1 ;;
  esac
}

# Split the command into segments on shell control operators so chained
# commands (a && b, a; b, a | b) are each inspected independently.
segments=$(printf '%s' "$command" | sed -E 's/(&&|\|\||;|\|)/\n/g')

dir=$cwd
while IFS= read -r segment; do
  read -ra tokens <<< "$segment"
  n=${#tokens[@]}
  [ "$n" -eq 0 ] && continue

  # Track `cd` so later segments know which repo they act on.
  if [ "${tokens[0]}" = "cd" ]; then
    if [ "$n" -ge 2 ]; then dir=$(abspath "${tokens[1]}" "$dir"); else dir=$HOME; fi
    continue
  fi

  for ((i = 0; i < n; i++)); do
    tok="${tokens[$i]}"
    [ "$tok" = "git" ] || continue

    # Walk past global git options (-C <path>, -c key=val, --git-dir=...)
    # to find the actual subcommand.
    j=$((i + 1))
    subcmd=""
    gitdir=$dir
    redirected=0
    while [ $j -lt $n ]; do
      t="${tokens[$j]}"
      case "$t" in
        -C) gitdir=$(abspath "${tokens[$((j + 1))]:-}" "$gitdir"); j=$((j + 2)); continue ;;
        -c) j=$((j + 2)); continue ;;
        --git-dir=* | --work-tree=* | --git-dir | --work-tree) redirected=1; j=$((j + 1)); continue ;;
        --namespace=*) j=$((j + 1)); continue ;;
        -*) j=$((j + 1)); continue ;;
        *) subcmd="$t"; break ;;
      esac
    done
    rest=("${tokens[@]:$((j + 1))}")
    restargs="${rest[*]:-}"

    case "$subcmd" in
      status|log|diff|show|describe|blame|shortlog|reflog|ls-files|ls-tree|\
      ls-remote|rev-parse|cat-file|grep|help|merge-base|name-rev|rev-list|\
      whatchanged|""|-h|--help|--version)
        : # read-only, allowed
        ;;
      branch)
        if printf '%s' "$restargs" | grep -qE '(^| )(-m|-M|-c|-C|--move|--copy)( |$)'; then
          deny "git branch with -m/-M/-c/-C is blocked (renames or copies a branch)"
        fi
        if printf '%s' "$restargs" | grep -qE '(^| )(-d|-D|--delete)( |$)'; then
          names=0
          for a in "${rest[@]}"; do
            case "$a" in
              -*) ;;
              *)
                b=$(unquote "$a")
                case "$b" in
                  worktree-agent-?*) names=$((names + 1)) ;;
                  *) deny "git branch -d/-D is only allowed for worktree-agent-* branches (got '$b')" ;;
                esac
                ;;
            esac
          done
          [ "$names" -gt 0 ] || deny "git branch -d/-D needs at least one worktree-agent-* branch name"
        fi
        ;;
      worktree)
        wsub=${rest[0]:-}
        case "$wsub" in
          list | prune) ;;
          add | remove)
            [ "$redirected" -eq 1 ] && deny "git worktree $wsub with --git-dir/--work-tree is blocked"
            path=""
            newbranch=""
            force=0
            k=1
            while [ $k -lt ${#rest[@]} ]; do
              a=${rest[$k]}
              case "$a" in
                -b | -B) newbranch=$(unquote "${rest[$((k + 1))]:-}"); k=$((k + 2)); continue ;;
                --reason) k=$((k + 2)); continue ;;
                -f | --force) force=1 ;;
                -*) ;;
                *) [ -z "$path" ] && path=$a ;;
              esac
              k=$((k + 1))
            done
            [ -z "$path" ] && deny "git worktree $wsub needs an explicit path under .claude/worktrees/"
            abs=$(abspath "$path" "$gitdir")
            in_agent_worktree "$abs" || deny "git worktree $wsub is only allowed for paths under .claude/worktrees/ (got '$path')"
            if [ "$wsub" = "remove" ] && [ "$force" -eq 1 ]; then
              deny "git worktree remove --force is blocked: commit the worktree's work to its branch first, then remove without --force"
            fi
            if [ -n "$newbranch" ] && ! is_agent_branch "$newbranch"; then
              deny "git worktree add -b is only allowed for research/* or worktree-agent-* branches (got '$newbranch')"
            fi
            ;;
          *)
            deny "git worktree $wsub is blocked (only list, prune, and add/remove under .claude/worktrees/)"
            ;;
        esac
        ;;
      switch)
        [ "$redirected" -eq 1 ] && deny "git switch with --git-dir/--work-tree is blocked"
        case "${rest[0]:-}" in
          -c | --create) ;;
          *) deny "git switch is only allowed as 'git switch -c research/<name>' inside an agent worktree" ;;
        esac
        b=$(unquote "${rest[1]:-}")
        case "$b" in
          research/?*) ;;
          *) deny "git switch -c is only allowed for research/* branches (got '$b')" ;;
        esac
        in_agent_worktree "$gitdir" || deny "git switch -c is only allowed inside an agent worktree (.claude/worktrees/)"
        ;;
      add | commit)
        [ "$redirected" -eq 1 ] && deny "git $subcmd with --git-dir/--work-tree is blocked"
        in_agent_worktree "$gitdir" || deny "git $subcmd is only allowed inside an agent worktree (.claude/worktrees/), not in the main checkout"
        current=$(git -C "$gitdir" branch --show-current 2>/dev/null)
        is_agent_branch "$current" || deny "git $subcmd is only allowed on research/* or worktree-agent-* branches (current: '${current:-detached}')"
        ;;
      *)
        deny "git $subcmd can change repository state and is blocked by project policy. Read-only commands (status, log, diff, show, branch/tag/remote listing, plain fetch) are allowed."
        ;;
    esac
  done
done <<< "$segments"

echo '{}'
