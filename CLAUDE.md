## Plan Mode

- Make the plan extremely concise. Sacrifice grammar for the sake of concision.
- At the end of each plan, give me a list of unresolved questions to answer, if any.
- For small changes and bugfixes (no full grill/spec/tickets flow): apply `/codebase-design` principles (deep modules, clean seams) while exploring and planning, then build test-first (`/tdd`-style, one red-green slice at a time).

## Agent skills

### Issue tracker

Issues/specs live as markdown files under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Domain docs

Multi-context: root `CONTEXT-MAP.md` points to per-context `CONTEXT.md` files. See `docs/agents/domain.md`.
