# Rebuild the warehouse into a new generation from the raw event store

The warehouse (player statistics and league table, in PostgreSQL) is a projection
of the raw event store (ADR-0037). An operator can recalculate it entirely, after
data corruption or a change to the calculation rules, without asking the other
squads to resend anything (US-DATA-03).

## Decision

- **Generations.** Every warehouse row carries a `generation_id`. A rebuild writes
  a new generation; `warehouse_state` names the active one. Reads only use the
  active generation, so a rebuild in progress is never visible: players keep
  seeing the last consolidated version. The switch is one transaction, once the
  rebuild is complete. The previous generation is kept until the next switch, so
  a read started before a switch still finds its rows.
- **Fixed bound.** A rebuild reads the events received before its request time
  (`consolidated_until`), sorted by receipt date. A resumed rebuild therefore reads
  exactly the events an uninterrupted one would.
- **Deduplication.** The raw store keeps redeliveries (ADR-0037). The rebuild
  counts each `messageId` once per generation, through the
  `warehouse_processed_messages` set written in the batch transaction.
- **Checkpoints.** Events are written in batches (`Warehouse:Rebuild:BatchSize`).
  Each batch commits its statistics, its message IDs and the rebuild checkpoint in
  one PostgreSQL transaction. The checkpoint is the receipt date of the last event
  read plus the IDs of every event read at that exact date, because several events
  can share one. Resuming restarts at that date and skips those IDs.
- **Exact sums.** Values are summed as `decimal` and stored as unbounded
  `numeric`: a resumed rebuild, whose batches differ, ends with exactly the totals
  of an uninterrupted one.
- **Execution.** `POST /api/v1/warehouse/rebuilds` records a pending rebuild; a
  background worker in each instance starts the oldest pending one. A unique
  partial index allows at most one pending or running rebuild, and the `xmin`
  concurrency token lets only one replica start it. A running rebuild without a
  heartbeat for `HeartbeatTimeout` lost its worker and is marked interrupted; the
  operator resumes it with `POST /api/v1/warehouse/rebuilds/{id}/resume`.
- **Report.** `GET /api/v1/warehouse/rebuilds/{id}` returns the events processed,
  applied, deduplicated and ignored, and the receipt date of the most recent one.
- **Authorization.** The rebuild endpoints require the global role configured in
  `Warehouse:OperatorRole` (default `operator`), read from the `X-Roles` header the
  gateway injects (ADR-GLOB-004).

## Considered Options

Rebuilding in place (truncate then recalculate) is simpler, but players would see
an empty or partial league table for the whole rebuild, and a failure would leave
it corrupted. Shadow tables swapped by `RENAME` avoid that but need DDL rights at
runtime and do not survive an interruption in the middle of the swap.

Deduplicating in memory would avoid writing the message IDs, but an interrupted
rebuild would lose the set and could count a redelivery twice after resuming.

## Consequences

During a rebuild the warehouse holds up to three generations: storage grows with
the number of players, not of events. Unused generations and deduplication sets
are deleted after each successful switch.

Events received after a rebuild's request time are not in its generation. Until an
incremental projection exists, the league table shows the data consolidated up to
the `consolidatedUntil` date it returns.

Writing three set-based statements per batch keeps the cost proportional to the
rows touched. The 5 million event budget is verified by
`WarehouseRebuildPerformanceTests`, which only runs when
`LEADERBOARD_PERF_EVENT_COUNT` is set.
