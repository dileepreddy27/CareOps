# Security policy

CareOps is portfolio/demo software and must not be used to store real provider documents, protected health information, or production credentials.

## Reporting

Please report a suspected vulnerability privately to the repository owner rather than opening a public issue. Include the affected endpoint or component, reproduction steps, impact, and any suggested mitigation. Do not include real sensitive data.

## Deployment minimums

Before an internet-accessible deployment: inject unique PostgreSQL and JWT secrets through a managed secret provider; terminate TLS; restrict CORS; disable Development seeding; adopt a cookie-based BFF or hardened token storage; implement MFA and privileged user provisioning; move documents to encrypted private object storage with scanning; add rate limiting and an outbox; and complete threat modeling, backup/restore testing, retention, audit, and compliance review.
