# 01: Shared provider tree + retry policy

**Spec:** [../spec.md](../spec.md)

**What to build:** The app and the test render helper share one provider tree and one query-client configuration, so tests run production behaviour. A transient server error when loading a Recipe or the Library is retried once; a client error is not retried.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] One query-client configuration factory: queries never retry a 4xx, retry any other failure once; mutations not retried
- [ ] One app-providers module renders query-client provider, router and toaster; app uses the browser router, `renderApp` a memory router with a fresh client per test
- [ ] No test builds its own query client with retries off
- [ ] Cook View: a 500 is requested twice, then shows "Rezept konnte nicht geladen werden."; a 500 that succeeds on retry shows the Recipe
- [ ] Library: a 4xx shows the error state after one request
- [ ] Retry delay short enough for the suite, same value in prod and tests
- [ ] Coverage gate passes
