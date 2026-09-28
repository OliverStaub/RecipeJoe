# Backend solution layout

Type: grilling
Status: resolved
Blocked by: —

## Question

What .NET projects make up `/backend`, where are the seams (Import fetcher, Import parser, Recipe store, image store), and which test project covers which layer? Apply `codebase-design` (deep modules, few seams). E.g. one API project + one core project vs vertical slices; test projects `*.UnitTests`, `*.IntegrationTests`, contract; shared Testcontainers fixture; how DI registration is organised.

## Answer

- **Projects**: single `src/RecipeJoe.Api` (host, Minimal API endpoints, domain, EF, adapters) with feature folders `Import/`, `Recipes/`, `Images/`. No Core project, no vertical-slice projects.
- **Seams**:
  - `IPageFetcher` (URL → content | failure) is the only interface. It covers all outbound HTTP, image downloads included. Adapters: HttpClient (prod), fixture (tests).
  - Parser: pure function (HTML → Recipe | ImportFailure), no interface.
  - Recipe store: `DbContext` directly, no repository.
  - Image store seam: decided in [Recipe image storage](06-recipe-image-storage.md). Add an interface only if two adapters really exist.
- **Import module**: deep `Importer.ImportAsync(url) → Result<Recipe, ImportFailure>` does fetch → parse → image download → save. The endpoint stays thin. Nothing is persisted on failure (enforced in one place).
- **Failures**: `ImportFailure` is a closed set (`Unreachable`, `NoRecipeFound`, `InvalidRecipe`) → `422` ProblemDetails with a `code` field, which the frontend maps to an inline message. Malformed URL → `400`.
- **Persistence**: `Recipes` table. Ingredient Lines and Steps are child tables via EF `OwnsMany` (`Position` int + `Text`), cascade-deleted, always loaded with the Recipe. `CreatedAt` via injected `TimeProvider` (`FakeTimeProvider` in tests).
- **Visibility**: `internal` by default. `InternalsVisibleTo` for UnitTests only. Integration tests go through HTTP only.
- **DI**: per-feature `AddImport()`/`AddRecipes()`/`AddImages()` + `MapImportEndpoints()` etc., called from `Program.cs`.
- **Test projects** (`backend/tests/`):
  - `RecipeJoe.UnitTests`: parser, Importer (fake fetcher via NSubstitute/fixture adapter), Import fixtures (saved HTML under `Fixtures/`).
  - `RecipeJoe.IntegrationTests`: one Postgres Testcontainer per assembly (`[AssemblyInitialize]`), migrations applied once, Respawn reset between tests, one shared `WebApplicationFactory` subclass swapping in the fixture fetcher.
  - `RecipeJoe.ContractTests`: OpenAPI drift; mechanics per [OpenAPI + TS client generator](02-openapi-ts-client-generator.md).
  - E2E lives outside `/backend`.
- **Build plumbing**: `backend/RecipeJoe.slnx`, `src/` + `tests/`. `Directory.Build.props` (warnings-as-errors, nullable, analyzers, coverage exclusions), `Directory.Packages.props` (central package versions, Renovate-friendly), `global.json` pinning the .NET 10 SDK.
