# Asynchronous broker selection for 100,000 concurrent players

> Decision and preliminary sizing note. The calculations are application
> assumptions, not benchmark results. Sources consulted on 22 September 2026.

## Decision: RabbitMQ is selected

**The project selects RabbitMQ with quorum queues for asynchronous flows to
Progression.** This confirms
[ADR-GLOB-002](../adr/ADR-GLOB-002-rabbitmq-as-message-broker.md) and covers
the 100,000 concurrent-player scenario with the sharded topology defined here.

The choice matches the actual need: short business facts routed to a limited
number of consumers, processed idempotently, with retries, dead-lettering and
short retention. Kafka is excellent for long retention, replay and many
independent readers, but those capabilities are not current product
requirements and would add operational scope.

Connected-player count is not a broker capacity unit. This decision is based on
throughput, message size, fan-out, persistence and acceptable lag. The
validation benchmark does not reopen the broker choice; it sizes the RabbitMQ
cluster and verifies the SLO before production.

## 1. What goes through the broker

Commands remain synchronous over SignalR/gRPC. The broker receives only facts
that were validated and written to an outbox.

| Family | Examples | Status |
|---|---|---|
| Combat action | `combat.action-resolved.v1`: attack, ability, defence, pass, potion | One fact per accepted, resolved action |
| Combat lifecycle | Combat start and completion | Statistics events |
| Session/run lifecycle | Session, room, chest, run completion | Statistics events |
| Reward/inventory | Reward, consumption, equipment, purchase | Statistics events |
| Snapshot, reconnect, rejected command, SignalR redelivery | `CombatStateChanged`, combat read | **Excluded**: no business value, unnecessary volume |

## 2. Calculation model

| Symbol | Meaning |
|---|---|
| `N` | Concurrent players |
| `f` | Fraction currently in combat |
| `a` | Resolved actions per player in combat per second |
| `o` | Other statistics facts per second |
| `P` | Publishes/s = `N × f × a + o` |
| `s` | Mean serialized size, including envelope |
| `B` | Incoming application bytes/s = `P × s` |

The measured size is the serialized Protobuf with its envelope (`messageId`,
correlation, causation, version and timestamp), not the C# object size.

| Message profile | Planning size |
|---|---:|
| IDs, action, counters/damage | 0.5 KiB |
| Target: envelope, targets and aggregate effects | 0.75 KiB |
| Snapshot, unbounded list, complete inventory | > 4 KiB — forbidden for statistics |

Events/s and MiB/s must be monitored together. Small messages increase event
rate; larger messages move the bottleneck towards disk and network. RabbitMQ
recommends benchmarking the real workload because size, clients, acks and
confirms change the outcome [How to run benchmarks](https://www.rabbitmq.com/blog/2020/06/04/how-to-run-benchmarks).

## 3. Calculations for 100,000 players

The figures use 0.5 then 0.75 KiB without compression, before protocol,
replication, retries, dead-lettering and consumer deliveries.

| Scenario | Assumption | `P` | At 0.5 KiB | At 0.75 KiB |
|---|---|---:|---:|---:|
| Quiet | One fact/player/minute | 1,667/s | 0.81 MiB/s | 1.22 MiB/s |
| Regular play | One fact/player/10 s | 10,000/s | 4.88 MiB/s | 7.32 MiB/s |
| Realistic combat | `N=100,000`, `f=30%`, `a=1`, `o≈3,500/s` | 33,500/s | 16.36 MiB/s | 24.54 MiB/s |
| Extreme peak | Every player takes one action/s, `o≈5,000/s` | 105,000/s | 51.27 MiB/s | 76.90 MiB/s |

```text
combat actions/s     = 100,000 × 0.30 × 1 = 30,000
other facts/s        ≈ 3,500
publishes/s          = 33,500
bytes/s at 0.75 KiB  = 33,500 × 768 = 25,728,000 B/s = 24.54 MiB/s
```

A three-replica quorum queue persists and replicates messages before a majority
confirm. Fan-out, confirms, acknowledgements and replays add work: size the
system using p95/p99 and end-to-end lag, never producer throughput alone.

## 4. Demonstrating the RabbitMQ choice

| Project criterion | Quantitative or functional observation | Why RabbitMQ meets it |
|---|---|---|
| Nominal throughput | 33,500 publishes/s in the realistic-combat scenario | Eight shards reduce the mean to 4,188/s/shard, far below the directional 80,000/s quorum-queue reference. |
| Peak to absorb | 105,000 publishes/s | Eight shards reduce the mean to 13,125/s/shard; the cluster is validated with peak and failure tests without changing broker model. |
| Message types | Small 0.5–0.75 KiB Protobuf facts; snapshots excluded | RabbitMQ queues, exchanges, bindings and backpressure directly support this message routing. |
| Consumers | Progression is the primary consumer; Player/Rewards receive only some facts | A durable queue per consumer isolates rates and failures without introducing a streaming platform. |
| Reliability | Outbox, confirms, manual acks, idempotence, retry and DLQ | Quorum queues provide majority replication and confirms; application deduplication covers at-least-once delivery. |
| Required ordering | Ordering only per hero/player, never globally | A stable hash to a shard preserves the business sequence without a global partition. |
| Operations | RabbitMQ is already the accepted ADR and planned broker | Kafka would add topics, partitions, retention, offsets and rebalancing without a matching product need. |

The 80,000/s reference is published by RabbitMQ for **one** quorum queue; it
must not be multiplied mechanically by eight because shards share cluster
resources. It nevertheless shows that the planned per-shard rates (4,188/s
nominal, 13,125/s peak) are of an order compatible with the quorum model.
Capacity proof remains the benchmark using the project's actual hardware,
schemas and guarantees.

## 5. RabbitMQ capacity and topology

Quorum queues are durable, replicated and safe. With three members, they
tolerate one lost node; publisher confirms arrive after a majority replication.
RabbitMQ advises against large fan-outs and very long backlogs (millions of
messages), where Streams are more appropriate
[Quorum queues](https://www.rabbitmq.com/docs/quorum-queues).

The official RabbitMQ/Kafka comparison gives an order of magnitude of **80,000
messages/s for one quorum queue**, replicating and `fsync`ing every message.
It is not a sizing guarantee: hardware, size, confirms, acks and topology alter
capacity [RabbitMQ vs Kafka](https://www.rabbitmq.com/docs/compare/kafka).

| Load | Progression topology | Conclusion |
|---|---|---|
| 1,667–10,000/s | Quorum queues | RabbitMQ is appropriate |
| ~33,500/s | Sharded RabbitMQ plus soak test | RabbitMQ is appropriate |
| ~105,000/s sustained | Distributed across eight shards | Sharded RabbitMQ, validated by a peak benchmark |

The project operational ceiling is **50% of measured durable capacity**, not
80,000/s. This internal margin absorbs failures, replays, spikes and growth.

Proposed topology:

1. Durable topic exchange `game.events.v1`.
2. Eight three-replica quorum queues: `progression.stats.0` through
   `progression.stats.7`.
3. Stable shard = hash of `HeroId`, otherwise `PlayerId`; order is preserved per
   key, never globally.
4. Outbox, publisher confirms, manual ack after writing Progression history,
   `messageId` plus business-key deduplication, bounded retry and DLQ.

Eight shards distribute roughly 33,500/s as 4,188/s/shard and 105,000/s as
13,125/s/shard before key imbalance. They avoid one leader for all traffic but
do not replace testing. Do not create a queue per player, combat or session:
RabbitMQ recommends reviewing a topology above roughly 5,000 quorum queues
[Quorum queues](https://www.rabbitmq.com/docs/quorum-queues).

## 6. Why Kafka and Streams are not selected now

Kafka is a partitioned, replayable log. Ordering is inside a partition, and a
consumer group assigns one partition to one consumer. Independent groups can
reread the same topic; this is preferable when Progression, BI, anti-cheat and
the data warehouse must retain and replay the same history at different rates
[Apache Kafka Design](https://kafka.apache.org/41/design/design/).

Kafka does not publish a universal messages/s capacity. Its throughput depends
on partitions, byte size, batching, compression, replication and hardware. Its
producer batches per partition; batching and compression improve throughput with
a latency trade-off [Kafka Producer configuration](https://kafka.apache.org/40/configuration/producer-configs/).

| Dominant need | RabbitMQ quorum queues | Kafka |
|---|---|---|
| Routing, ack/retry/DLQ to a few services | Excellent | Possible, more operational scope |
| Retention/replay for weeks or months | Poor fit | Excellent |
| Many independent consumers | One queue per consumer increases fan-out | Natural through consumer groups and offsets |
| Large backlog / fan-out | Prefer Streams or Kafka | Native |
| Current MVP | Already selected and simpler to operate | More concerns: partitions, retention, offsets |

Kafka does not remove the need for idempotence in the Progression database:
exactly-once is chiefly a Kafka-to-Kafka mechanism; a projection to an external
database still needs coordination or application-level deduplication
[Apache Kafka Design](https://kafka.apache.org/41/design/design/).

RabbitMQ Streams is the middle ground: a persistent, replicated and replayable
log suited to large fan-outs, replays and large backlogs. Super streams add
partitioning [Streams and Superstreams](https://www.rabbitmq.com/docs/streams).

## 7. RabbitMQ validation benchmark

The test uses the actual Protobuf schemas, outbox, confirms and Progression
writes; a synthetic generator alone is insufficient.

| Step | Load | Acceptance |
|---|---|---|
| Baseline | 10,000/s for 30 min | Stable backlog, no DLQ, p99 measured |
| Target | 33,500/s for 2 h | Stable lag, CPU/disk/network within budget |
| Peak | 105,000/s for 15 min | Controlled backpressure, no loss |
| Soak | Target for 24 h | No memory, disk, backlog or p99 drift |
| Failure | Lose one node and restart a consumer | Zero double XP; recovery within SLO |

Measure publishes/s and MiB/s per shard, confirm latency p50/p95/p99, backlog
size/age, redelivery/DLQ, outbox-to-projection latency, disk, network, CPU, GC
and connections. Use stable local SSD/NVMe for RabbitMQ: the production
checklist recommends fast local storage for quorum queues and Streams
[RabbitMQ Production checklist](https://www.rabbitmq.com/docs/production-checklist).

## 8. Future reassessment conditions

RabbitMQ is this architecture's selected broker. Reassess it only if the
product requires retention/replay as a feature, at least three independent
analytics consumers, a normal backlog of millions of messages, or tests prove a
correctly sharded RabbitMQ cluster cannot meet the SLO. These conditions describe
a future needs change, not uncertainty about the present decision.

## Primary sources

- [RabbitMQ — RabbitMQ vs Kafka](https://www.rabbitmq.com/docs/compare/kafka)
- [RabbitMQ — Quorum queues](https://www.rabbitmq.com/docs/quorum-queues)
- [RabbitMQ — Streams and Superstreams](https://www.rabbitmq.com/docs/streams)
- [RabbitMQ — How to run benchmarks](https://www.rabbitmq.com/blog/2020/06/04/how-to-run-benchmarks)
- [RabbitMQ — Production checklist](https://www.rabbitmq.com/docs/production-checklist)
- [Apache Kafka — Design](https://kafka.apache.org/41/design/design/)
- [Apache Kafka — Producer configuration](https://kafka.apache.org/40/configuration/producer-configs/)
