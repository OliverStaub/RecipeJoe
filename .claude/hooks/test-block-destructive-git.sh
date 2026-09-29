#!/usr/bin/env bash
# Tests for block-destructive-git.sh. Run: bash .claude/hooks/test-block-destructive-git.sh
set -uo pipefail

HOOK="$(cd "$(dirname "$0")" && pwd)/block-destructive-git.sh"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

R="$tmp/repo"
W="$R/.claude/worktrees/agent-1"
F="$R/.claude/worktrees/agent-feature"
P="$tmp/plain"
{
  git init -q -b main "$R"
  git -C "$R" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
  git -C "$R" worktree add -q "$W" -b worktree-agent-1
  git -C "$R" worktree add -q "$F" -b feature
  git init -q -b main "$P"
  git -C "$P" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
  git -C "$P" checkout -q -b feature-x
} || { echo "setup failed"; exit 1; }

pass=0
fail=0
check() {
  local expected=$1 cwd=$2 cmd=$3 out got
  out=$(jq -n --arg c "$cmd" --arg d "$cwd" '{tool_input:{command:$c},cwd:$d}' | bash "$HOOK")
  if printf '%s' "$out" | grep -q '"deny"'; then got=deny; else got=allow; fi
  if [ "$got" = "$expected" ]; then
    pass=$((pass + 1))
  else
    fail=$((fail + 1))
    echo "FAIL: expected $expected, got $got | cwd=${cwd#"$tmp"/} | $cmd"
  fi
}

# Read-only and existing policy
check allow "$R" "git status"
check allow "$R" "git log --oneline"
check deny "$R" "git push"
check deny "$W" "git push origin research/x"
check deny "$W" "git reset --hard"
check deny "$W" "git checkout -b research/x"

# add / commit only in agent worktree on agent branch
check deny "$R" "git add ."
check deny "$R" "git commit -m x"
check allow "$W" "git add research/a.md"
check allow "$W" "git commit -m 'Add research'"
check allow "$R" "git -C .claude/worktrees/agent-1 commit -m x"
check allow "$R" "cd .claude/worktrees/agent-1 && git add a && git commit -m x"
check deny "$R" "cd .claude/worktrees/agent-1 && cd ../../.. && git commit -m x"
check deny "$W" "cd $R && git commit -m x"
check deny "$W" "git --git-dir=$R/.git commit -m x"

# add/commit allowed anywhere on a non-main branch, worktree or not
check allow "$F" "git commit -m x"
check allow "$P" "git add ."
check allow "$P" "git commit -m x"

# switch -c: any non-main branch name, anywhere
check allow "$W" "git switch -c research/foo"
check allow "$W" "git switch -c research/foo && git add f && git commit -m x"
check allow "$R" "git switch -c research/foo"
check allow "$W" "git switch -c feature/foo"
check allow "$P" "git switch -c feature/foo"
check deny "$R" "git switch -c main"
check deny "$P" "git switch -c master"
check deny "$W" "git switch main"
check deny "$W" "git switch -C research/foo"

# worktree
check allow "$R" "git worktree list"
check allow "$R" "git worktree prune"
check allow "$R" "git worktree add .claude/worktrees/agent-2 -b worktree-agent-2"
check allow "$R" "git worktree add $R/.claude/worktrees/agent-2 -b research/x"
check deny "$R" "git worktree add ../elsewhere -b worktree-agent-3"
check deny "$R" "git worktree add .claude/worktrees/agent-3 -b feature"
check allow "$R" "git worktree remove .claude/worktrees/agent-1"
check deny "$R" "git worktree remove --force .claude/worktrees/agent-1"
check deny "$R" "git worktree remove -f .claude/worktrees/agent-1"
check deny "$R" "git worktree remove /tmp/x"
check deny "$R" "git worktree remove .claude/worktrees/../../x"
check deny "$R" "git worktree move .claude/worktrees/agent-1 .claude/worktrees/agent-9"

# branch deletion only for worktree-agent-*
check allow "$R" "git branch --list"
check allow "$R" "git branch -D worktree-agent-1"
check allow "$R" "git branch -d worktree-agent-1 worktree-agent-2"
check deny "$R" "git branch -D main"
check deny "$R" "git branch -D worktree-agent-1 main"
check deny "$R" "git branch -D research/x"
check deny "$R" "git branch -m worktree-agent-1 main"

echo "passed: $pass, failed: $fail"
[ "$fail" -eq 0 ]
