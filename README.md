# .NET Backend Service Template

**Démarrage local : [guide rapide en français](docs/demarrage-local.md).**

**Collecte d'événements et contrats producteurs : [index collection](docs/collection/README.md).**

**Variables RabbitMQ et MongoDB pour l'OPS : [exemples de configuration](docs/ops/README.md).**

Deployment parameters and target service communications are documented in
[deployment configuration](docs/deployment-configuration.md).

Starting point for a backend microservice: a .NET 10 Clean Architecture solution,
the CI pipeline that guards it, and the branching flow that releases it.

Vocabulary is defined in [CONTEXT.md](./CONTEXT.md). Shared technical choices are
recorded in [BACKEND_TECHNICAL_DECISIONS.md](./docs/BACKEND_TECHNICAL_DECISIONS.md),
and the decisions behind this repository's own shape in [docs/adr](./docs/adr).

## Structure

```text
Leaderboard.Domain/          entities, enums, domain services, repository interfaces
Leaderboard.Application/     commands, queries, handlers, validators, pipeline behaviours
Leaderboard.Infrastructure/  EF Core, repository implementations, external services
Leaderboard.Presentation/    HTTP API: controllers, DTOs, middleware
Leaderboard.Contracts        owned Protobuf contracts for Leaderboard events
Leaderboard.Test/             xUnit tests for all of the above
```

`Presentation` is the Clean Architecture layer name for the HTTP API. There is no
user interface.

## Commands

```powershell
dotnet tool restore
dotnet restore Leaderboard.Presentation.slnx
dotnet build Leaderboard.Presentation.slnx
dotnet test --solution Leaderboard.Presentation.slnx
dotnet run --project Leaderboard.Presentation/Leaderboard.Presentation.csproj
```

## Dependencies

All current dependencies restore from nuget.org. No GitHub token is required.
The central collection contract is defined in `leaderboard.collection.v1`; it is
independent of the legacy `leaderboard.events.v1`. See the five producer guides
under [docs/collection](docs/collection/README.md). The format is available here;
external squad adoption is not asserted.

## Running the stack

```bash
docker compose up -d --build
```

The API listens on <http://localhost:8080>, Postgres on host port 5433, and the
RabbitMQ management UI on <http://localhost:15672> (`leaderboard` / `leaderboard`). Because
`ASPNETCORE_ENVIRONMENT` is `Development`, the OpenAPI document is served at
`/openapi/v1.json` and the Scalar UI at `/scalar`.

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
docker compose down -v   # -v also drops the database volume
```

Apply the EF Core migrations before calling endpoints that persist data:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project Leaderboard.Infrastructure --startup-project Leaderboard.Infrastructure
```

## Toolchain

The SDK version is pinned in `global.json`; `dotnet tool restore` installs the
coverage collector, EF Core Tools, CSharpier, and the git-hook runner declared in
`.config/dotnet-tools.json`.
Run `dotnet husky install` once per clone to enable the pre-commit hook — git
hook paths are local configuration and cannot be committed.

## Configuration

PostgreSQL is configured through the `ConnectionStrings` section.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=leaderboard;Username=leaderboard",
    "PasswordFile": "/run/secrets/postgres_password"
  }
}
```

`PasswordFile` is optional. It injects the password from a Docker or Kubernetes
secret instead of storing it in the configuration file.

## Database migrations

`Leaderboard.Infrastructure` owns both the migrations and the design-time
`LeaderboardDbContextFactory`, including the EF Core Design dependency. The factory
loads the Presentation configuration from the repository root and lets
`ConnectionStrings__DefaultConnection` override it.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> --project Leaderboard.Infrastructure --startup-project Leaderboard.Infrastructure
dotnet tool run dotnet-ef database update --project Leaderboard.Infrastructure --startup-project Leaderboard.Infrastructure
```

Development startup applies them before seeding. Production-like deployments must
run migrations as a controlled rollout step, never by every application instance.

For host-based development, `appsettings.Development.json` targets the Compose
PostgreSQL port `5433`. The Compose API uses its own `postgres:5432` connection.

## Asynchronous messaging

The canonical ingestion contract is `leaderboard.collection.v1.GameEventEnvelope`,
with 16 typed event routes documented in [the catalogue](docs/collection/README.md).
The consumer and immutable MongoDB journal come from PR #10; the warehouse comes from PR #14.
The complete AMQP body is archived before ACK, including invalid/unknown envelopes.
See [integration decisions](docs/collection/integration-pr-10-14.md),
[deployment configuration](docs/deployment-configuration.md) and [OPS notes](docs/ops/README.md).

## Warehouse rebuild

The warehouse (player statistics and league table) is recalculated from the raw
event store; the design is in
[ADR-0038](./docs/adr/0038-rebuild-the-warehouse-into-a-new-generation-from-the-raw-event-store.md).
The rebuild endpoints require the `operator` role in the `X-Roles` header.

| Endpoint | Does |
| --- | --- |
| `POST /api/v1/warehouse/rebuilds` | Requests a full rebuild (202, or 409 if one is pending or running) |
| `POST /api/v1/warehouse/rebuilds/{id}/resume` | Resumes an interrupted or failed rebuild from its checkpoint |
| `GET /api/v1/warehouse/rebuilds/{id}` | Report: status, events processed, applied, deduplicated, ignored, most recent one |
| `GET /api/v1/players/{playerId}/statistics` | Statistics of a player, from the last consolidated generation |
| `GET /api/v1/leaderboard?page=1&pageSize=20` | League table, from the last consolidated generation |

```bash
curl -X POST -H "X-Roles: operator" http://localhost:8080/api/v1/warehouse/rebuilds
```

The performance test seeds millions of events, so it only runs on demand:

```powershell
$env:LEADERBOARD_PERF_EVENT_COUNT = "5000000"
dotnet test --solution Leaderboard.Presentation.slnx --filter-class "*WarehouseRebuildPerformanceTests"
```

## Branching flow

```text
feature/xxx --merge commit--> dev --merge commit--> main --> tag + CHANGELOG
                         ^                      |
                         +----- back-merge -----+
```

- `dev` is the default branch. Open every feature pull request against it and
  merge it with a **merge commit**: every commit keeps its author and its own
  line in the history.
- Promote by opening a pull request from `dev` to `main`, also merged with a
  **merge commit**. Never squash or rebase — release-please reads the individual
  commits ([ADR-0002](./docs/adr/0002-merge-strategy-depends-on-the-target-branch.md)).
- release-please then maintains independent release pull requests on `main` for
  the application and the Protocol Buffer contracts. Merging one writes its
  changelog, bumps only its version and tags it (`vX.Y.Z` for the application,
  `leaderboard-contracts-vN.0.0` for contracts).
- A back-merge from `main` to `dev` follows automatically
  ([ADR-0003](./docs/adr/0003-automatic-back-merge-from-main-to-dev.md)).

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org):
`feat:` and `fix:` appear in the changelog and move the version, everything else
(`chore:`, `ci:`, `refactor:`, `test:`, `docs:`, `build:`, `style:`) is hidden and
moves nothing. Since every commit reaches `main`, every commit message is checked:
by a `commit-msg` hook locally and by the `Commitlint` job in CI. The full rules
are in [docs/GIT_RULES.md](./docs/GIT_RULES.md).

## Continuous integration

| Workflow | Runs on | Does |
| --- | --- | --- |
| `ci.yaml` | PR to `dev` / `main`, push to `main` | Calls the reusable lint, test and build workflows |
| `commitlint.yaml` | PR to `dev` / `main` | Checks every commit message of the pull request |
| `sonar.yaml` | PR and push to `dev`, except Dependabot | Builds and tests under the SonarScanner for .NET, uploads coverage |
| `security.yml` | PR to `dev` / `main`, push to `main` | Trivy filesystem scan, zizmor workflow audit |
| `release-please.yaml` | push to `main` | Maintains the release pull request |
| `publish-contracts.yaml` | `leaderboard-contracts-v*` tags | Checks the package and publishes it to GitHub Packages |
| `back-merge.yaml` | after a release | Opens and merges `main` → `dev` |

Formatting is enforced by `dotnet csharpier check .`. CSharpier runs locally on
staged C# files through Husky.Net, alongside a `commit-msg` hook checking the
Conventional Commits format. Run `dotnet tool restore` then `dotnet husky install`
once per clone.

## Integration tests

`Leaderboard.Test/Integration` contains a runnable REST controller example
using `WebApplicationFactory`, PostgreSQL and Respawn.
Start the database with `docker compose up -d postgres`, then run:

```powershell
dotnet test --solution Leaderboard.Presentation.slnx --filter "FullyQualifiedName~Integration"
```

Each fixture creates and drops a unique database, then applies the service
migrations. Set
`LEADERBOARD_TEST_DATABASE_CONNECTION` to use another administrative PostgreSQL
connection; it is never reset itself.

## Setting up a new repository from this template

1. Create the repository **public** (SonarQube Cloud's free tier requires it).
2. Import the organisation into SonarQube Cloud and create the project, then:
   - **Branches**: delete the `dev` entry SonarQube Cloud created on its own,
     then rename the main branch from `main` to `dev` — the rename is refused
     while a branch of that name already exists. The free plan covers one
     long-lived branch, and `dev` is the one that matters (ADR-0005).
   - **Administration → Analysis method**: switch **Automatic Analysis off**.
     Left on, it competes with the scanner and every CI analysis fails.
   - **Administration → New code**: number of days, 30.
3. Add `SONAR_TOKEN` and `BOT_TOKEN` as secrets. `BOT_TOKEN`, not the default
   `GITHUB_TOKEN`: a pull request opened by the latter triggers no workflow, so
   the release pull request would never get a CI run.
4. Set `dev` as the default branch and protect both `dev` and `main`. Required
   checks: `Lint / CSharpier`, `Test / dotnet test`, `Build / dotnet build`,
   `Trivy Security Scan`, `GitHub Actions audit`, `Commitlint`. **Not** `SonarQube Cloud scan`:
   it is skipped on Dependabot pull requests, and a required check that never
   runs blocks them forever. Keep "require linear history" **off**, or the merge
   commits this flow depends on become impossible.
5. Add one ruleset on `dev` and `main` allowing **merge commits** only, so the
   merge strategy is enforced rather than merely written down (ADR-0007). No
   bypass is needed. Disable squash and rebase merging in the repository
   settings too.
6. Enable auto-merge on the repository; the back-merge workflow uses it.
7. Rename the template projects to your service name, and update `/k:` and
   `/o:` in `.github/workflows/sonar.yaml`.

## Contrats et intégration des événements

Le format de référence est `leaderboard.collection.v1.GameEventEnvelope` :
[contrat et fiches producteurs](docs/collection/README.md).
Les cinq services .NET 10 utilisent le package partagé `Leaderboard.Contracts` :
[installation et publication](docs/collection/package-nuget.md).
Le consommateur et le stockage MongoDB proviennent de la PR #10 ; la projection
PostgreSQL et sa reconstruction proviennent de la PR #14.
Voir [décisions de fusion](docs/collection/integration-pr-10-14.md) et
[configuration](docs/deployment-configuration.md).
