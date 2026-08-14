# Contributing

1. Create a focused branch and keep domain changes covered by tests.
2. Run `dotnet build CareOps.sln -c Release`, `dotnet test CareOps.sln -c Release`, `npm run lint`, and `npm run build` before opening a pull request.
3. Add an ADR when a change alters a system boundary, security posture, persistence strategy, or operational model.
4. Never commit credentials, `.env`, real provider data, or credential binaries. Use synthetic fixtures only.
5. Keep API changes reflected in OpenAPI, `http/CareOps.Api.http`, and the README.
