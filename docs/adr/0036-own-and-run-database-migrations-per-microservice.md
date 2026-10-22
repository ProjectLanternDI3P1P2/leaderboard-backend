# Own and run database migrations per microservice

Each microservice owns and versions its Entity Framework Core migrations. The
shared template contains no migration files because its generated service must
create an initial migration for its own domain model.

Development startup and integration-test fixtures apply migrations. Deployed
environments use a dedicated migration execution mechanism or service.

## Considered Options

Shipping an initial migration in a template makes a generated service inherit a
schema history that is unrelated to its actual domain. Skipping migrations after
the service is created would make schema evolution unsafe and non-repeatable.

Sharing migrations between services would contradict database ownership and
couple independently evolving schemas.

A dedicated migration execution step keeps schema changes explicit and separate
from serving application traffic.

## Consequences

Migration files live with the persistence code of the microservice that owns the
database. Production-like deployments must execute them before or as a controlled
part of rollout.

## GitOps migration command

The published API image accepts `--migrate`. It applies this service's pending
EF migrations and exits without starting the API or seeding Development data.
A missing migration assembly or database/configuration error exits nonzero.
Normal production startup still does not migrate the database.

GitOps runs this mode in a dedicated PreSync Job using the same immutable image
as the Deployment, with schema-specific connection settings and a mounted
password file. Existing EF migration locking serializes runs for the same
migration history. Review schema compatibility with the currently running API
before release; rolling back an image never rolls back the schema.

The test workflow exercises the published command on a disposable PostgreSQL
database, including repeat execution and missing-credentials failure. No
production database is used by that check.
