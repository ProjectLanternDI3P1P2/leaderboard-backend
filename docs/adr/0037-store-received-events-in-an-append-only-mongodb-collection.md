# Store received events in an append-only MongoDB collection

Every integration message the service receives is stored as a `RawEvent` in a
MongoDB collection (`progression_lake.raw_events`) before it is acknowledged.
The store is the source of truth: projections can be emptied and rebuilt from it
without asking the other squads to resend anything.

`IRawEventStore` exposes only `AppendAsync` and `GetByPeriodAsync`. Immutability
is enforced by design in the code (no update or delete in the contract) and by
the database (the application role may only `insert` and `find`).

## Decisions

- Writes use `InsertMany` with `w: majority` and `journal: true`. A batch is
  acknowledged to the broker only after this write is confirmed.
- `receivedAt` is set by the consumer on receipt, before batching, and is never
  altered by the store.
- Indexes on `{ receivedAt, _id }`, `transactionId` and `messageId` are
  non-unique. There is no TTL index, no capped collection and no uniqueness
  constraint, so duplicates and old events are always kept.
- The collection, indexes and schema validator are created by
  `RawEventCollectionInitializer`, run with a migration account. The application
  role cannot create indexes. In Development the service runs it at startup, like
  the EF Core migrations.
- Period reads filter `from <= receivedAt < to` and sort by `(receivedAt, _id)`.

## Consumer retry and dead-letter convention

ADR-GLOB-002 leaves retry and dead-letter handling to the broker configuration.
For this consumer:

- A quorum queue binds the five producer exchanges by default, with `BindingKey=#`.
  `RawEventConsumer:SourceExchanges` selects direct exchanges; an empty list uses
  `RabbitMq:ExchangeName` for a preprovisioned aggregate exchange.
  Quorum dead-lettering uses at-least-once delivery to the durable parking queue,
  through the default exchange, with no TTL.
- Messages are consumed in batches. On a failed write the batch is retried with
  exponential backoff up to `MaxAttempts`; it is not acknowledged meanwhile.
- After storage retries are exhausted, messages are rejected without requeue
  and retained in the parking queue. Unreadable/invalid envelopes are instead
  archived with their validation status and original bytes.
- A failed attempt may have inserted part of a batch, so a retry can store an
  event twice. Retries generate fresh receipt IDs to avoid duplicate `_id` failures.
  Duplicates are accepted: replays deduplicate on `messageId`
  (ADR-0025).

## Consequences

Delivery is at least once. The transaction identifier is read from the canonical
`leaderboard.collection.v1.GameEventEnvelope`. The stored `payload` contains the
complete AMQP body, including the envelope, unchanged. Extracted fields can be null
for unreadable bodies; `parseStatus` and `validationErrors` explain their state.
See [integration decisions](../collection/integration-pr-10-14.md).
Backup, restore, deployment of the replica set and bypass detection (change
stream watcher) are operational work tracked separately from the code.
