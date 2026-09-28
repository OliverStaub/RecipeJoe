#!/usr/bin/env bash
# PreToolUse guard for the Bash tool: blocks git invocations that would
# change repository state (commit, push, merge, reset, clean, add, etc.)
# Allows read-only git commands (status, log, diff, show, branch/tag/remote
# listing, plain fetch, config --get, ...).
#
# This is a heuristic firewall, not a sandbox: it inspects the literal
# command string. It cannot catch every possible obfuscation (e.g. a
# base64-encoded command, or git invoked from inside a script file it
# then executes), but it blocks the direct, common ways Claude Code would
# run a mutating git command.
set -uo pipefail

input=$(cat)
command=$(printf '%s' "$input" | jq -r '.tool_input.command // empty')

[ -z "$command" ] && { echo '{}'; exit 0; }

deny() {
  jq -n --arg reason "$1" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$reason}}'
  exit 0
}

# Split the command into segments on shell control operators so chained
# commands (a && b, a; b, a | b) are each inspected independently.
segments=$(printf '%s' "$command" | sed -E 's/(&&|\|\||;|\|)/\n/g')

while IFS= read -r segment; do
  read -ra tokens <<< "$segment"
  n=${#tokens[@]}
  for ((i = 0; i < n; i++)); do
    tok="${tokens[$i]}"
    [ "$tok" = "git" ] || continue

    # Walk past global git options (-C <path>, -c key=val, --git-dir=...)
    # to find the actual subcommand.
    j=$((i + 1))
    subcmd=""
    while [ $j -lt $n ]; do
      t="${tokens[$j]}"
      case "$t" in
        -c|-C) j=$((j + 2)); continue ;;
        --git-dir=*|--work-tree=*|--namespace=*) j=$((j + 1)); continue ;;
        -*) j=$((j + 1)); continue ;;
        *) subcmd="$t"; break ;;
      esac
    done
    restargs="${tokens[*]:$((j + 1))}"

    case "$subcmd" in
      status|log|diff|show|describe|blame|shortlog|reflog|ls-files|ls-tree|\
      ls-remote|rev-parse|cat-file|grep|help|merge-base|name-rev|rev-list|\
      whatchanged|""|-h|--help|--version)
        : # read-only, allowed
        ;;
      branch)
        if printf '%s' "$restargs" | grep -qE '(^| )(-d|-D|-m|-M|-c|-C|--delete|--move|--copy)( |$)'; then
          deny "git branch with -d/-D/-m/-M/-c/-C is blocked (renames or deletes a branch)"
        fi
        ;;
      tag)
        if printf '%s' "$restargs" | grep -qE '(^| )(-d|--delete)( |$)'; then
          deny "git tag -d is blocked (deletes a tag)"
        fi
        if [ -n "$restargs" ] && ! printf '%s' "$restargs" | grep -qE '^(-l|--list)?$'; then
          deny "git tag is only allowed for listing (-l/--list or no args); creating a tag is blocked"
        fi
        ;;
      remote)
        if [ -n "$restargs" ] && ! printf '%s' "$restargs" | grep -qE '^(-v|show( .*)?)$'; then
          deny "git remote is only allowed for viewing (-v / show), not add/remove/set-url/rename"
        fi
        ;;
      config)
        if ! printf '%s' "$restargs" | grep -qE '(^| )(--get|--get-all|--get-regexp|--list|-l)( |=|$)'; then
          deny "git config is only allowed for reading (--get/--list), not setting values"
        fi
        ;;
      fetch)
        if printf '%s' "$restargs" | grep -qE '(^| )(--prune|-p|--force|-f)( |$)'; then
          deny "git fetch --prune/--force is blocked (can rewrite local refs)"
        fi
        ;;
      submodule)
        if ! printf '%s' "$restargs" | grep -qE '^(status|summary|foreach)( |$)?'; then
          deny "git submodule mutating subcommands (update/add/deinit/...) are blocked"
        fi
        ;;
      *)
        deny "git $subcmd can change repository state and is blocked by project policy. Read-only commands (status, log, diff, show, branch/tag/remote listing, plain fetch) are allowed."
        ;;
    esac
  done
done <<< "$segments"

echo '{}'
