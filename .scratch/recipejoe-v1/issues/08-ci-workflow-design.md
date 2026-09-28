# CI workflow design

Type: grilling
Status: open
Blocked by: 03, 04, 05, 13

## Question

How are the GitHub Actions workflows structured to run the agreed stages (lint/format → build → unit → integration → coverage gate → E2E)? Jobs vs steps, parallel backend/frontend lanes, `needs` graph, caching, browser matrix, coverage merge + job summary, artifacts kept (test reports, Playwright traces), triggers (push to main, PRs, nightly?), runner-minute budget.
- From [V1 screens](09-v1-screens.md): the Cook View uses the Screen Wake Lock API, which only exists in a secure context. If E2E browsers reach the app at a non-localhost http origin (e.g. `http://web` on the compose network), `navigator.wakeLock` is undefined. Either serve E2E on `localhost` or assert only the "unavailable" fallback there.
