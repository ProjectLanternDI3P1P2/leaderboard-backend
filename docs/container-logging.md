# Container application logs

Production Serilog events go to the console as one JSON object per line. The
application does not send logs to Loki; the container log collector owns that
transport. The console sink has no ANSI theme or second text sink.

| Field | Meaning |
| --- | --- |
| `timestamp` | ISO 8601 date and time with a numeric UTC offset |
| `level` | `debug`, `information`, `warning`, `error`, or `fatal` |
| `service` | `leaderboard-backend` |
| `origin` | `backend` |
| `message` | Rendered Serilog message |
| `properties` | Structured event properties, including `SourceContext` and `CorrelationId` when present |
| `exception` | Optional object with `type`, `message`, and `stack` (stack may be null) |

Credential and personal-data property names are removed, including nested
properties; known bearer tokens, JWTs, and email addresses in structured string
values are redacted. Log call sites must still avoid putting secrets in literal
message templates or exception messages.

Ops can collect stdout/stderr using the existing Kubernetes container-log path.
No application environment variable or Loki endpoint is required.
