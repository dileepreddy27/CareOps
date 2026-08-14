# ADR 003: Use an idempotent hosted compliance worker

- **Status:** Accepted
- **Date:** 2026-08-14

## Context

Expiration warnings and SLA escalations are time-based and must survive retries without notifying users repeatedly.

## Decision

Run an hourly `BackgroundService`. Each notification has a unique business deduplication key, and profile expiration uses the same domain method as interactive workflow changes. Failures are logged and retried on the next interval.

## Consequences

The MVP has few moving parts and safe retries. Before multiple API replicas are used, add a PostgreSQL advisory lock or external scheduler so only one scanner owns a run. Notification delivery beyond SignalR should use an outbox.
