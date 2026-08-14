# Performance and reliability measurement plan

CareOps includes a repeatable k6 scenario at `scripts/load-test.js`. It authenticates once and exercises the dashboard and bounded provider queue—the two read paths most likely to appear in a resume bullet.

## Baseline command

```bash
docker compose up -d --build
docker run --rm -e BASE_URL=http://host.docker.internal:5080 -v "$PWD/scripts:/scripts" grafana/k6:latest run /scripts/load-test.js
```

On Linux, use host networking or an explicit Compose network instead of `host.docker.internal`.

## Acceptance thresholds

| Signal | MVP target | Why |
|---|---:|---|
| Dashboard/queue p95 | < 300 ms | Interactive operations target |
| HTTP failure rate | < 1% | Detects auth, DB pool, and timeout failures |
| Ready probe | 100% during run | Detects database connectivity loss |
| API image cold start | < 15 s after healthy PostgreSQL | Practical Compose/deployment feedback |

Record the machine, commit SHA, dataset size, VUs, duration, p50/p95/p99, throughput, and failure rate with every result. Do not turn a local result into a resume claim without this context.

## Observed local baseline

Measured on 2026-08-14 against the exact Compose image identified below. This is a local engineering baseline, not a production capacity claim.

| Context | Value |
|---|---|
| Host | Windows NT 10.0.26200, x64, 12 logical processors |
| Runtime | Docker Engine 29.3.1, Linux containers |
| Source commit | `cf92cb2` |
| API image | `sha256:2c6d452346f8195b736741463b9135bb60f25e4e24b6af1a7de170ca16c6bb04` |
| Dataset | Six seeded provider profiles with credentials, checklists, comments, audit events, and shifts |
| Scenario | 20 constant VUs for 30 seconds; one authenticated dashboard read and one queue read per iteration |
| Volume | 1,201 HTTP requests; 39.00 requests/second |
| Combined read latency | 12.81 ms average, 7.29 ms median, 31.57 ms p90, **43.38 ms p95**, 74.98 ms maximum |
| Dashboard p95 | 46.95 ms |
| Provider queue p95 | 39.43 ms |
| Failures | **0.00%**; 1,201/1,201 checks passed |
| Stability | Readiness stayed healthy and the API restart count remained zero |

The host was otherwise idle and completed SDK build workers were stopped before measurement. Rerun the scenario on the eventual Git commit and target hosting environment before using these numbers outside the portfolio README.

## Reliability checks already automated

- Testcontainers provisions a fresh PostgreSQL database and applies the committed migration.
- Health readiness executes an EF Core database check; liveness is process-only.
- PostgreSQL transient retry is bounded at three attempts.
- Optimistic concurrency detects clashing provider updates.
- Compliance alerts use unique dedupe keys so retrying a scan is safe.
