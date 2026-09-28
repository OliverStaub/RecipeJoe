# OpenAPI contract pipeline: backend emitter + TS client generator

Researched 2026-09-28. Question: which backend OpenAPI emitter and which frontend generator should RecipeJoe use for a contract-test layer where drift fails CI?

## Recommendation

**Backend:** `Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server` (build-time emission) on .NET 10, with the spec committed to the repo.
**Frontend:** `openapi-typescript` (types only) + `openapi-fetch` + `openapi-react-query`.
**CI:** regenerate spec and types, then run `git diff --exit-code` over both.

Why:
- It's first-party, it ships with the framework, and `dotnet build` writes the spec to disk, so no runtime server is needed in CI.
- The generated TS is a single `.d.ts` file, so there's no generated runtime code to exclude from coverage, lint or `noUnusedLocals`. The runtime is about 6 kb plus about 1 kb.
- `openapi-typescript` 7.x is the most stable of the three generators. `@hey-api/openapi-ts` is pre-1.0 and has had breaking releases roughly monthly. Orval is solid but generates a lot of runtime code.

Choose Orval instead if you want generated hooks per endpoint, or Zod/MSW mocks, and accept a much larger generated surface.

## Backend emitters

| | Microsoft.AspNetCore.OpenApi | Swashbuckle.AspNetCore | NSwag |
|---|---|---|---|
| Latest stable | 10.0.12 (11.0.0-rc.1 in preview) [N1] | 10.2.3, 2026-06-22 [G1] | 14.7.1, 2026-04-20 [G2] |
| .NET support | Built in since .NET 9. Emits OpenAPI 3.1 by default on .NET 10 and 3.2 on .NET 11 [D1] | net8.0 / net9.0 / net10.0 TFMs [N2]. ASP.NET Core >= 8 [G3] | net8.0 / net9.0 / net10.0 (+ netstandard2.0, net462) [N3]. v14.6.0 added .NET 10 [G2] |
| OpenAPI 3.1 | Yes (Microsoft.OpenApi 2.0) [D1][N4] | Yes, since v10 (Microsoft.OpenApi 2.x) [G3] | No documented 3.1 support. Issue #3761 was closed without it [G4] |
| Build-time file | `Microsoft.Extensions.ApiDescription.Server`, runs during `dotnet build` [D1] | `Swashbuckle.AspNetCore.Cli` (`swagger tofile`) [G3] | `NSwag.MSBuild` / nswag CLI |
| Maintenance | Microsoft, same release train as ASP.NET Core | Active again (Martin Costello plus dependabot; five releases in 2026) [G1]. Removed from templates in .NET 9 because it was unmaintained at the time [I1] | Active (commit on 2026-09-07), but led mainly by one maintainer |

Notes:
- **Swashbuckle** is no longer abandoned. However, Microsoft moved the templates off it [I1], and it adds nothing we need over the built-in package for Minimal APIs.
- **NSwag's** strength is its C#/TS client generators, not document emission. Its 3.0-only schema model is a poor fit for 3.1-native tooling.

### Built-in build-time generation, the details that matter [D1]
- Add the `Microsoft.Extensions.ApiDescription.Server` package. After that, `dotnet build` runs a **GetDocument** step and writes `{ProjectName}.json` to the output directory.
- `<OpenApiDocumentsDirectory>.</OpenApiDocumentsDirectory>` writes the file next to the csproj instead. Use this so the file can be committed.
- `<OpenApiGenerateDocumentsOptions>--file-name openapi --document-name v1 --openapi-version OpenApi3_1</OpenApiGenerateDocumentsOptions>` pins the name and version.
- The step **runs the app's entry point with a mock server**, so all startup code runs. Guard DB and secret wiring with `Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider"`. This matters for EF Core and Postgres registration.
- The logs are hidden by the terminal logger. Use `dotnet build -tlp:v=d` or `--tl:off` to see them.
- .NET 11 adds `<OpenApiGenerationEnvironment>`.

### Gotcha: integers typed as `integer | string`
ASP.NET Core defaults `JsonNumberHandling` to `AllowReadingFromString`. As a result, `int` and `long` are emitted as `type: ["integer","string"]` with a digit `pattern`, and TS then sees `number | string`. The fix is to set `NumberHandling = Strict` via `ConfigureHttpJsonOptions` for Minimal APIs [D2][I2]. Do this on day one, because it changes every generated numeric type.

## Frontend generators

| | openapi-typescript + openapi-fetch + openapi-react-query | orval | @hey-api/openapi-ts |
|---|---|---|---|
| Latest | 7.13.0 / 0.17.0 / 0.5.4 (2026-02-11) [G5][P1] | 8.38.0 (2026-09-26). v8.0.0 shipped 2026-01-14 [G6][P1] | 0.99.0 (2026-06-22) [P1] |
| What is generated | **Types only**, in one `.d.ts` file. "Generates only TypeScript type definitions… no runtime code" [W1] | Per-endpoint fetcher, query key fn and `useX` hook, plus optional Zod/MSW/Faker [W3] | SDK functions, types, and via plugins `xOptions()`, `xMutation()`, query keys and infinite options [W4] |
| TanStack integration | `$api.useQuery("get", "/recipes/{id}", {params})`, plus `useMutation`, `useSuspenseQuery`, `useInfiniteQuery`, `queryOptions`. Peer dependency `@tanstack/react-query ^5.80` [W1][P1] | `client: 'react-query'` generates one hook per path, with `useInfinite`, `skipToken`, and cache get/set helpers [W3] | Generates `queryOptions`/`mutationOptions` objects that you pass to TanStack's own hooks. Supports React, Vue, Svelte, Solid, Preact and Angular (experimental) [W4] |
| Runtime size | openapi-fetch "weighs 6 kb" [W2]. openapi-react-query is a "tiny wrapper (1 kb)" [W1] | Your fetch/axios plus the generated code, which grows with the endpoint count | Bundled client plus generated SDK, which grows with the endpoint count |
| Generated-code footprint | Minimal. Nothing to exclude from coverage except one `.d.ts` | Large. Needs coverage and lint exclusions | Large. Needs coverage and lint exclusions |
| Stability | Mature 7.x line. Supports OpenAPI 3.0 and 3.1 [G7]. Runtime packages are still 0.x | Mature 8.x line with frequent minor releases | "In initial development. Please pin an exact version" [W5]. Breaking minors: 0.85 to 0.99 in about 9 months, including ESM-only (0.91) and a Node bump (0.96) [W6][P1] |
| Node | 20+ recommended [G7] | >= 22.18 [P1] | >= 22.18 [P1] |

Trade-off: openapi-react-query means no generated hooks. Calls are keyed by the path string, for example `"/recipes/{id}"`, and that path is type-checked against the spec. If the backend renames a route, the frontend fails `tsc`, which is exactly the contract signal we want. Generating only types also means "warnings are errors" never trips on generated code.

## CI drift check, end to end

1. **Backend (source of truth).** `dotnet build -warnaserror` runs GetDocument and rewrites `backend/<Api>/openapi.json`, which is committed.
2. **Frontend.** `npx openapi-typescript ../backend/<Api>/openapi.json -o src/api/schema.d.ts`, run as an npm script such as `gen:api`. The output is committed.
3. **Drift gate.** `git diff --exit-code -- backend/<Api>/openapi.json frontend/src/api/schema.d.ts`. Any change means someone edited endpoints or DTOs without committing the regenerated contract, so the job fails and prints the diff.
4. **Type gate.** `tsc --noEmit` checks frontend code against the fresh `schema.d.ts`. A removed or renamed field or route becomes a compile error.
5. Run both gates in one GitHub Actions job with the .NET 10 SDK and Node 22. Alternatively, have the backend job upload `openapi.json` as an artifact and let the frontend job consume it.
6. **Determinism.** Pin generator versions exactly (lockfile) and pin `--openapi-version`. Keep the build-time startup free of environment-dependent config, so the same code always produces byte-identical output.

Locally, a single `gen:api` script (`dotnet build` followed by `openapi-typescript`) keeps developers in sync. A pre-commit hook is optional.

## Sources
- [D1] Microsoft Learn, "Generate OpenAPI documents" (aspnetcore-10.0, updated 2026-09-05): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi
- [D2] Microsoft Learn, "Include OpenAPI metadata" (NumberHandling): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0
- [I1] dotnet/aspnetcore #54599, Swashbuckle removed from .NET 9 templates: https://github.com/dotnet/aspnetcore/issues/54599
- [I2] dotnet/aspnetcore #64145, integers as integer/string: https://github.com/dotnet/aspnetcore/issues/64145
- [N1] NuGet flat-container index for microsoft.aspnetcore.openapi and microsoft.extensions.apidescription.server: https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/index.json
- [N2] Swashbuckle.AspNetCore.SwaggerGen 10.2.3 nuspec: https://api.nuget.org/v3-flatcontainer/swashbuckle.aspnetcore.swaggergen/10.2.3/swashbuckle.aspnetcore.swaggergen.nuspec
- [N3] NSwag.AspNetCore 14.7.1 nuspec: https://api.nuget.org/v3-flatcontainer/nswag.aspnetcore/14.7.1/nswag.aspnetcore.nuspec
- [N4] Microsoft.AspNetCore.OpenApi 10.0.0 nuspec (depends on Microsoft.OpenApi 2.0.0): https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/10.0.0/microsoft.aspnetcore.openapi.nuspec
- [G1] Swashbuckle releases: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/releases
- [G2] NSwag releases: https://github.com/RicoSuter/NSwag/releases
- [G3] Swashbuckle README: https://github.com/domaindrivendev/Swashbuckle.AspNetCore
- [G4] NSwag #3761 "Is OpenAPI 3.1.0 supported?": https://github.com/RicoSuter/NSwag/issues/3761
- [G5] openapi-typescript releases: https://github.com/openapi-ts/openapi-typescript/releases
- [G6] orval releases: https://github.com/orval-labs/orval/releases
- [G7] openapi-typescript README: https://github.com/openapi-ts/openapi-typescript/blob/main/packages/openapi-typescript/README.md
- [P1] npm registry (`npm view` versions, times, engines, peerDependencies), queried 2026-09-28
- [W1] openapi-react-query docs: https://openapi-ts.dev/openapi-react-query/
- [W2] openapi-fetch docs: https://openapi-ts.dev/openapi-fetch/
- [W3] orval React Query guide: https://orval.dev/docs/guides/react-query
- [W4] Hey API TanStack Query plugin: https://heyapi.dev/openapi-ts/plugins/tanstack-query
- [W5] Hey API get started: https://heyapi.dev/openapi-ts/get-started
- [W6] Hey API migrating guide: https://heyapi.dev/openapi-ts/migrating
