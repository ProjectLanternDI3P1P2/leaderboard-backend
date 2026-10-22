# Backend Testing Strategy

> Shared testing strategy for all backend microservices.
>
> This document describes what should be tested and at which level. The testing
> libraries themselves are recorded in `BACKEND_TECHNICAL_DECISIONS.md`.

# Principles

Tests must focus on observable behavior and business correctness.
Prefer the lowest test level that gives meaningful confidence.
Do not reproduce the same scenario at every level without a reason.
Use Arrange / Act / Assert consistently.

# Unit tests

Unit tests cover isolated domain and application behavior.

Typical targets include domain rules, validators, handlers with mocked external
dependencies and small infrastructure-independent services.

EF Core InMemory may be used for fast tests where the goal is application logic,
not validation of actual PostgreSQL behavior.

An EF Core InMemory test is not considered a persistence integration test.

# Integration tests with the real database

Persistence integration tests MUST use the real PostgreSQL engine used by the
service.

The test database is reset before each test with Respawn.
Each test seeds only the data required by its scenario.

These tests may start directly from an application handler and continue through
the real repositories and EF Core DbContext until changes are persisted.

This is the preferred integration-test level for most application use cases
because it validates handler orchestration, repository implementation, EF Core
mapping, relational constraints, PostgreSQL behavior and final persisted state.

A large part of the integration test suite may use this level.

# Entry-point integration tests

A smaller number of integration tests start from an actual service entry point:

- HTTP endpoint;
- gRPC endpoint;
- asynchronous message consumer.

These tests exercise the complete path from the external contract to the database
or resulting application effect.

They are more expensive to write and maintain, so they should cover representative
contract behavior rather than every business permutation.

For an important endpoint or consumer, prefer approximately:

- one successful green-path test;
- two or three important failure-path tests.

Useful failure paths include invalid input, resource not found, forbidden business
state, conflict and duplicate processing.

# Database lifecycle

Integration tests run against a dedicated test database.
The test fixture applies the service migrations before tests run.
Respawn resets application data between tests while keeping the schema available.
Tests must not depend on execution order.

# Test data

Use explicit data when exact values matter to the scenario.
Use Bogus when realistic generated data reduces repetitive setup.
Generated data must not make tests nondeterministic.

# Assertions and mocking

Use FluentAssertions for readable assertions.
Assert externally meaningful results and persisted state.
Use Moq for dependencies where isolation is intended.
Do not mock simple domain objects.
Do not mock the database in tests whose purpose is to validate persistence.

# Coverage

Coverage is a diagnostic indicator, not a target by itself.
Critical business behavior must be tested even when aggregate coverage is high.
Do not add low-value tests only to increase a coverage percentage.
