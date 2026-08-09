# Open Measurement Evidence Toolkit

Open Measurement Evidence Toolkit is a small, offline-first .NET library for validating and comparing reproducible Windows PC performance evidence. It provides strict, resource-bounded JSON handling, canonical evidence hashes, versioned schemas, PresentMon-compatible CSV analysis, and conservative repeated-run comparisons.

The project reads evidence. It does not capture telemetry, tune a PC, change system settings, inject into games, bypass anti-cheat, or redistribute PresentMon.

## Current status

The code is a pre-release extraction undergoing public-readiness review. The API and schemas may change before version 1.0. This package is now licensed as Apache-2.0.

## License

Open Measurement Evidence Toolkit is licensed under the Apache License 2.0 (ASL-2.0). See [LICENSE](LICENSE) for the full text.

## Quick publish (one click)

From Windows Explorer, double-click:

- [scripts/publish-one-click.bat](scripts/publish-one-click.bat)

That launcher will prompt for a GitHub token once per run and then publish `main` and `v0.1.0-rc.1`.

From a PowerShell prompt:

```powershell
.\scripts\publish-one-click.ps1
```

## Build and test

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build OpenMeasurementEvidence.sln --configuration Release
dotnet run --project tests/OpenMeasurementEvidence.Tests/OpenMeasurementEvidence.Tests.csproj --configuration Release
```

The test runner uses synthetic inputs only and exits nonzero on failure.

## Design principles

- Fail closed on ambiguous, malformed, oversized, or internally inconsistent evidence.
- Keep capture and system mutation outside the library boundary.
- Treat one run as evidence, not a conclusion; comparisons require repeated compatible runs.
- Separate observations, validation failures, and decision rules.
- Avoid network calls, background services, analytics, and model/API dependencies.

See [SECURITY.md](SECURITY.md) for the threat boundary, [PROVENANCE.md](PROVENANCE.md) for source history, and [ROADMAP.md](ROADMAP.md) for release gates.

## Non-affiliation

PresentMon is referenced only as an interoperable CSV format. This project is not affiliated with, endorsed by, or sponsored by Intel or the PresentMon project.
