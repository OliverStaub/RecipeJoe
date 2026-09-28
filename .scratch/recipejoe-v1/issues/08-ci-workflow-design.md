# CI workflow design

Type: grilling
Status: open
Blocked by: 03, 04, 05, 13

## Question

How are the GitHub Actions workflows structured to run the agreed stages (lint/format → build → unit → integration → coverage gate → E2E)? Jobs vs steps, parallel backend/frontend lanes, `needs` graph, caching, browser matrix, coverage merge + job summary, artifacts kept (test reports, Playwright traces), triggers (push to main, PRs, nightly?), runner-minute budget.
