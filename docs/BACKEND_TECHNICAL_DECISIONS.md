# Backend Technical Decisions

> This document records backend technical decisions shared by all five microservices.
>
> It answers **"what do we use?"** and carries conventions that no tool can enforce.
> Repository-specific architectural decisions live in [docs/adr](./docs/adr).
> Testing strategy lives in [BACKEND_TESTING_STRATEGY.md](./BACKEND_TESTING_STRATEGY.md).
> Vocabulary lives in [CONTEXT.md](./CONTEXT.md).

---

# Confirmed decisions

| Area | Decision |
|---|---|
| Language | C# |
| Runtime | .NET 10 |
| HTTP API | ASP.NET Core Controllers |
| HTTP API project suffix | `Presentation` |
| Architecture | Clean Architecture |
| Repository model | One repository per microservice |
| Shared backend library | None |
| Object mapping | Manual and explicit |
| Persistence | Entity Framework Core |
| Database | PostgreSQL |
| Repository style | Domain-specific repositories |
| Application pattern | CQRS |
| Mediator | MediatR |
| Command transaction boundary | One command = one transaction, enforced by MediatR behavior |
| Validation | FluentValidation |
| Error handling | Exceptions + global error middleware |
| Error response format | ProblemDetails |
| Validation error HTTP status | 422 |
| Identifier type | Guid |
| Date/time type | DateTimeOffset |
| Domain finite states | C# enums |
| Client-facing API style | REST |
| Synchronous inter-service communication | gRPC |
| API versioning | Version in URL |
| Initial API version | `v1` |
| JSON serializer | System.Text.Json |
| JSON property naming | camelCase |
| API enum serialization | String |
| API date/time format | ISO 8601 |
| Successful response envelope | None |
| Pagination | `page` + `pageSize` |
| API documentation | Scalar |
| Authentication | Keycloak |
| Logging | Serilog |
| Distributed observability | OpenTelemetry |
| Asynchronous message broker | RabbitMQ |
| Async message format | Protocol Buffers |
| Async message metadata | Common envelope |
| Message contract versioning | Mandatory |
| Exchange, routing key and queue naming | Common convention |
| Unit test framework | xUnit |
| Test SDK | Microsoft.NET.Test.Sdk |
| Mocking | Moq |
| Test data | Bogus |
| Assertions | FluentAssertions |
| Integration database reset | Respawn |
| Configuration | `IOptions<T>` |
| Containers | Dockerfile per service |
| Health checks | `/health/live`, `/health/ready` |
| Code formatting | Standard .NET + repository `.editorconfig` |
| Static analysis | SonarQube Cloud |
| Code coverage | `dotnet-coverage` |
| Dependency updates | Dependabot |
| Branching model | git flow: `dev` integrates, `main` releases |
| Release automation | release-please, Conventional Commits |

---

# CQRS and transaction conventions

Commands modify state. Queries read state.

A command/query SHOULD represent one clear use case.

Controllers SHOULD communicate with Application through MediatR.

Commands are transaction boundaries. A MediatR behavior owns the persistence
commit for successful commands.

Repositories MUST NOT call `SaveChangesAsync` themselves.

A command handler may modify several aggregates or repositories through the same
scoped DbContext. If the handler succeeds, the transaction behavior persists the
changes once. If the handler fails, no command-level commit is performed.

Queries do not use the command transaction behavior.

---

# Mapping

Object mapping is manual and explicit.

Do not introduce AutoMapper, Mapster, Mapperly or another mapping library as a
shared project convention.

Mapping code SHOULD remain close to the boundary where the conversion is needed
and MUST remain easy to discover during code review.

---

# API contracts

API transport models SHOULD be separate from Domain entities.

Do not expose EF Core entities directly through controllers.

Successful responses return the response DTO directly. Do not wrap successful
responses in a generic `data` envelope.

System.Text.Json is the standard serializer.

HTTP JSON uses camelCase properties, string enums and ISO 8601 date/time values.

Collection endpoints that require pagination use `page` and `pageSize`.

Validation failures return HTTP 422 with ProblemDetails-compatible structured
field errors using an `errors` dictionary.

---

# API versioning policy

Leaderboard's client-facing HTTP APIs begin with `/api/leaderboard/v1/...`, so
the gateway can route the service by prefix. See ADR 0019 for older services.

Backward-compatible changes remain in the current major version.
A breaking contract change requires a new major version.

---

# Inter-service communication

Client-facing APIs use REST.
Synchronous communication between backend microservices uses gRPC.
Asynchronous communication follows the shared messaging conventions; broker
selection remains a separate global architecture decision.

---

# Database migrations

Each microservice owns and versions its own EF Core migrations.

Developers create migrations with EF Core tooling when the service data model
changes.

Application startup MUST NOT automatically run `Database.Migrate()` outside a
development environment.

Deployed environments use a dedicated migration execution mechanism or service.

---

# Tests

The detailed strategy is documented in
[BACKEND_TESTING_STRATEGY.md](./BACKEND_TESTING_STRATEGY.md).

EF Core InMemory does not replace integration tests against the real PostgreSQL
database.

---

# Open backend decisions

The following backend-development choices are intentionally not fixed yet:

- caching strategy;
- optimistic concurrency strategy;
- soft-delete strategy;
- generic auditing strategy.

These subjects should only be standardized once a real project need requires a
shared decision.

---

# Decision rule

A technology choice affecting multiple repositories must be discussed as a
project-wide decision before teams independently introduce incompatible solutions.

The absence of a shared library does not mean the absence of shared standards.
