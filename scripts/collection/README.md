# Real collection acceptance run

This local integration benchmark publishes 30,000 valid Protobuf bodies over 600 seconds at a scheduled cadence of 50/s, observes publisher confirms and Mongo insert receipts, then compares stored bytes and metadata and checks backlog/quarantine. It uses isolated resources; see the source and report location below for exact behavior. Run only when a ten-minute workload is explicitly requested.

The accepted latency criterion is strict: last-minute publication-to-Mongo-insert p95 must be no greater than first-minute p95, and the fitted slope must be <=0 ms/min. Cadence lateness must be <=200 ms. Other checks require 30,000 published, confirmed and stored, exact bytes, no mismatch, metadata error, backlog or DLQ item. Criteria are not relaxed automatically.

After requirements and local services are ready, start `docker compose up -d postgres rabbitmq mongo` and run:

```powershell
dotnet build scripts/collection/Collection.Load.csproj --configuration Release --maxcpucount:1 -p:UseSharedCompilation=false
dotnet run --project scripts/collection/Collection.Load.csproj --configuration Release --no-build
```

There is no automatic rerun. A run stopped before completion has no final acceptance report and cannot validate the full 600-second criterion. JSON evidence is written under `scripts/collection/results/<run-id>.json`; receipts remain in that run's Mongo database for inspection. Successful runs remove only their own broker resources; failures retain them. Do not purge shared volumes.

R3 completed 600 seconds and stored/confirmed all 30,000 bodies, but result was FAIL: p95 improved and slope was negative, while maximum publication schedule lateness was 2592.7408 ms (>200). R4 changed the generator to cadence-based asynchronous confirms, but was stopped by user request at approximately three minutes (9000 sent, 8999 confirmed, one pending); no final report exists. The complete 50/s, 600-second criterion remains unvalidated. The user's confirmed p95 requirements are final <= initial and slope <=0; <=200 ms is the benchmark's cadence threshold. No further long run without a future explicit request.
