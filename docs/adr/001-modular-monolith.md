# ADR 001: Start with a modular monolith

- **Status:** Accepted
- **Date:** 2026-08-14

## Context

Credentialing decisions update profiles, checklists, credentials, comments, audit events, notifications, and scheduling eligibility. These operations benefit from one transaction boundary, and the MVP does not have independent scaling evidence for any module.

## Decision

Use one ASP.NET Core deployment with separate Domain, Application, Infrastructure, API, and Web projects. Keep interfaces at the application boundary and prevent Infrastructure from leaking into the domain.

## Consequences

Deployment and local development stay simple, and invariants remain transactional. If compliance scanning or scheduling later needs independent scaling, the application interfaces and persisted audit stream provide extraction seams. This design does not claim microservice failure isolation.
