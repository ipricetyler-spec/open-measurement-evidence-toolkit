# Maintainers

Primary maintainer: `ipricetyler-spec`.

This public repository accepts focused issues and pull requests that stay within the offline evidence-validation boundary described in [README.md](README.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [SECURITY.md](SECURITY.md).

Maintainer responsibilities include issue triage, pull-request review, release management, security response, compatibility decisions, and keeping project claims aligned with evidence.

## Local maintenance and publish helpers

Run the deterministic maintenance workflow before proposing a release or publishing repository changes:

```powershell
.\scripts\run-open-source-workflow.ps1
```

Run only the repository health preflight:

```powershell
.\scripts\open-source-health.ps1
```

On systems without the .NET 8 SDK, `-SkipBuild` permits boundary/schema checks only. That mode is not equivalent to a successful full build/test verification and must not be reported as one.

The repository also contains one-click Windows helpers for routine repository publishing. Those helpers do not override release authorization, signing boundaries, required test evidence, or a documented stop-on-failure gate.

## Release discipline

A release candidate must be tied to an exact source revision and its verification evidence. If a material release gate fails, preserve that failed cycle as evidence; do not silently rebuild, re-sign, retag, or represent a later artifact as the same reviewed candidate. A later material attempt is a new release cycle and should be recorded as such.
