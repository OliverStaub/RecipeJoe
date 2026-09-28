# OpenAPI + TS client generator

Type: research
Status: resolved
Blocked by: —

## Question

How should the backend emit its OpenAPI document and the frontend generate a typed client, so that API drift breaks the build (contract layer)? Compare built-in `Microsoft.AspNetCore.OpenApi` (build-time document generation) vs Swashbuckle/NSwag, and TS generators openapi-typescript (+ openapi-fetch), orval, hey-api — fit with TanStack Query, generated-code size, CI drift check (regenerate + `git diff --exit-code`). Recommend one pipeline.

Research: [`research/openapi-ts-client-generator.md`](../../../research/openapi-ts-client-generator.md) (no branch: repo guardrail blocks agent git writes)

## Answer

- Backend: built-in `Microsoft.AspNetCore.OpenApi` (.NET 10, OpenAPI 3.1) + `Microsoft.Extensions.ApiDescription.Server` → `dotnet build` writes a committed `openapi.json`. Not Swashbuckle (adds nothing), not NSwag (no documented 3.1 support).
- Frontend: `openapi-typescript` (types-only `.d.ts`) + `openapi-fetch` + `openapi-react-query`. Near-zero generated code to exclude. Rejected: hey-api (pre-1.0, frequent breaks), orval (heavy codegen).
- CI drift check: build backend → regenerate types → diff-exit-code on both files → `tsc --noEmit`.
- Gotcha: set JSON `NumberHandling = Strict`, else ints become `number | string`.
