# MVP milestone plan

| Milestone | Exit criteria | Status |
|---|---|---|
| 1. Architecture and domain | Modular solution, workflow transition map, approval/expiry invariants, ADRs | Complete |
| 2. Secure API and persistence | PostgreSQL migration, Identity/JWT roles, validated endpoints, seed personas | Complete |
| 3. Realtime operations | SignalR workflow events, compliance scanner, durable deduplicated alerts, SLA dashboard | Complete |
| 4. Full-stack experience | Responsive React operations dashboard and provider portal, scheduling view | Complete |
| 5. Verification and release | Unit/integration tests, Compose, CI, API examples, load harness, public README | Complete |

## Finishable scope decisions

- Binary credential upload is deliberately behind an abstraction; the MVP accepts safe metadata and hashes instead of pretending local disk is production object storage.
- The background scanner is an in-process hosted service with idempotent persistence. A multi-replica deployment would add distributed job leasing before scaling horizontally.
- The queue uses database projections and bounded pagination. Search is intentionally limited to provider name and NPI for the MVP.
- One deployable application serves the compiled SPA. This avoids cross-origin production complexity while Vite remains independently runnable in development.
