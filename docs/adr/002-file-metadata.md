# ADR 002: Store credential metadata, not binaries

- **Status:** Accepted
- **Date:** 2026-08-14

## Context

Credential documents can contain sensitive personal information. A public portfolio repository must not contain or casually serve them, while the domain still needs provenance, integrity, issue/expiry dates, and review status.

## Decision

Persist an opaque storage key, normalized display filename, allow-listed content type, bounded size, and SHA-256 digest. `IFileMetadataStorage` generates non-public keys. The demo has no binary upload/download endpoint.

## Consequences

The workflow can be demonstrated safely. A production adapter can add presigned object-storage upload, malware scanning, encryption, retention, and access logging without changing the aggregate. End-to-end content verification remains future production work.
