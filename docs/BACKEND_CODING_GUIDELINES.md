# Backend Coding Guidelines

> Shared coding and organization rules for all backend microservices.
>
> Naming rules are documented separately in `BACKEND_NAMING_CONVENTIONS.md`.

## General principles

Backend code must favor:

- readability;
- explicit behavior;
- simple solutions;
- small cohesive classes;
- clear business boundaries;
- testability;
- observability;
- consistency across microservices.

Avoid unrelated refactors and unnecessary abstractions.

## Solution structure

Each microservice uses:

```text
<Service>.Domain/
<Service>.Application/
<Service>.Infrastructure/
<Service>.Presentation/
<Service>.Test/
```

Dependencies point inward:

```text
Presentation
 ├──> Application
 └──> Infrastructure

Infrastructure
 ├──> Application
 └──> Domain

Application
 └──> Domain

Domain
 └──> nothing
```

## Domain

The Domain project contains business concepts and business rules.

Typical structure:

```text
Entities/
Enums/
Exceptions/
Repositories/
Services/
```

Add folders such as `ValueObjects/` or `Events/` only when needed.

Domain must not depend on ASP.NET Core, Entity Framework Core, message brokers, HTTP clients, logging implementations, or configuration providers.

Business rules must not live in controllers or infrastructure code.

## Application

Application contains use cases and orchestration.

Use feature-first organization:

```text
Features/
├── WarehouseRebuildUseCase/
│   └── StartWarehouseRebuild/
│       ├── StartWarehouseRebuildCommand.cs
│       └── StartWarehouseRebuildCommandHandler.cs
└── StatisticsUseCase/
    └── GetLeagueTable/
        ├── GetLeagueTableQuery.cs
        ├── GetLeagueTableQueryHandler.cs
        └── GetLeagueTableValidator.cs
```

Handlers may coordinate domain logic and infrastructure abstractions.

Handlers must not contain HTTP-specific logic, use concrete infrastructure clients, or access `DbContext` directly.

Controllers communicate with Application through MediatR.

## Infrastructure

Infrastructure contains technical implementations.

Typical structure:

```text
Persistence/
Services/
InfrastructureServiceRegistration.cs
```

Add technical folders such as `Messaging/`, `ExternalServices/`, or `Identity/` when needed.

Infrastructure may contain:

- Entity Framework Core;
- PostgreSQL access;
- repository implementations;
- external service clients;
- messaging implementations;
- authentication adapters.

Infrastructure must not contain business rules.

## Presentation

Presentation contains the HTTP API.

Current structure:

```text
Controllers/
DTO/
Extensions/
Middleware/
Program.cs
```

Controllers must remain thin:

1. receive the HTTP request;
2. create the corresponding command or query;
3. send it through MediatR;
4. return the HTTP response.

Do not place business logic, persistence logic, or repeated error handling in controllers.

## Validation

Use FluentValidation for input validation.

Input shape and request constraints belong in validators.

Critical business invariants must also be enforced in Domain or Application logic.

## Error handling

Use centralized exception handling middleware.

Controllers must not contain repeated `try/catch` blocks for standard application errors.

Use `ProblemDetails` for HTTP errors.

Default mappings:

| Situation | HTTP |
|---|---:|
| Authentication failure | 401 |
| Resource not found | 404 |
| Conflict / invalid state | 409 |
| Validation failure | 422 |
| Unexpected technical error | 500 |

Do not expose stack traces or internal exception details in production.

Unexpected exceptions must be logged.

## REST APIs

Use resource-oriented routes.

Prefer:

```text
GET  /api/v1/leaderboard
GET  /api/v1/players/{playerId}/statistics
POST /api/v1/warehouse/rebuilds
```

Avoid action-oriented routes such as:

```text
POST /api/v1/startWarehouseRebuild
GET  /api/v1/getPlayer
```

Rules:

- all APIs start at `/api/v1/...`;
- route resources use lowercase plural nouns;
- use explicit route identifiers when useful;
- do not expose EF Core entities directly;
- keep transport models in `Presentation/DTO`.

Backward-compatible additions stay in the current API version.

Breaking contract changes require a new major API version.

## Dependency injection

Use constructor injection.

Do not use `IServiceProvider` as a service locator in Domain or Application code.

Register dependencies close to the layer that owns them.

Use layer registration entry points such as:

```csharp
services.AddApplication();
services.AddInfrastructure(configuration);
```

## Configuration

Access configuration through strongly typed `IOptions<T>` models.

Prefer:

```csharp
IOptions<KeycloakOptions>
```

over scattered direct configuration access.

Validate required configuration at startup when invalid configuration prevents correct service operation.

## Entity Framework Core

Keep EF Core concerns in Infrastructure.

Prefer Fluent API configuration instead of persistence attributes in Domain entities.

Database-specific behavior must not leak into controllers or Domain logic.

## Logging and observability

Use structured logging.

Prefer:

```csharp
logger.LogInformation(
    "Player {PlayerId} started combat {CombatId}",
    playerId,
    combatId);
```

Do not use string interpolation for structured log properties.

Pass exceptions as exception parameters.

Never log secrets, passwords, tokens, or sensitive authentication data.

Preserve tracing and correlation context across service boundaries.

Add useful business identifiers to logs and traces when relevant.

## CancellationToken

Propagate `CancellationToken` through asynchronous Application and Infrastructure operations when cancellation is meaningful.

Controllers must pass the token to MediatR.

## Messaging

Messages are serialized as JSON and use a versioned contract.

Distributed messages use common metadata concepts:

```json
{
  "messageId": "guid",
  "correlationId": "guid",
  "causationId": "guid",
  "messageType": "CombatCompleted",
  "version": 1,
  "occurredAt": "2026-09-02T12:00:00+00:00",
  "producer": "combat",
  "payload": {}
}
```

Backward-compatible additions remain in the same message version.

Breaking changes require a new version.

Handlers that may process duplicate operations must protect business invariants.

Use `MessageId` or a business idempotency key where appropriate.

## Remote calls and resilience

For every remote operation, consider:

- cancellation;
- timeout;
- transient failures;
- retry safety;
- idempotency;
- cascading failures;
- degraded behavior.

Do not add retries blindly.

A retry is only acceptable when the operation is safe to repeat or protected by idempotency.

## Tests

Use:

- xUnit;
- Moq;
- Bogus;
- FluentAssertions.

Tests should focus on behavior.

Use clear Arrange / Act / Assert separation.

Use Moq for dependency behavior, not for simple domain objects.

Use Bogus when realistic test data improves readability.

## Formatting and comments

Follow standard .NET formatting and the repository `.editorconfig`.

Do not reformat unrelated files.

Comments should explain why, not repeat what the code already says.

## Final rule

When two approaches are equally valid, prefer the one already used consistently in the project.
