# 08: Renovate

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §4.2 (Renovate)

**What to build:** Automated dependency updates that keep Actions pinned and automerge safe bumps when CI is green.

**Blocked by:** 07 (CI workflow + coverage gate)

**Status:** ready-for-agent

- [ ] `renovate.json`: `helpers:pinGitHubActionDigests`, weekly schedule, groups (`@types/*`, MSTest, …), `minimumReleaseAge: 3 days`
- [ ] Automerge minor/patch/digest when green; majors as PRs; runner label bumps included
- [ ] Config passes `renovate-config-validator`
