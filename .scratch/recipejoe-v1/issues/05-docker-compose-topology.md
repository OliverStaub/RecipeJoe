# docker-compose topology

Type: grilling
Status: open
Blocked by: 04

## Question

Which services does `docker-compose.yml` define (postgres, backend, frontend, …), and how do dev and E2E use it? Dev: hot reload (dotnet watch / Vite dev server) in containers vs only Postgres in compose and apps on host. E2E: built images composed in CI runner, overrides file, healthchecks, ports, env/secrets handling, DB reset between E2E runs.
