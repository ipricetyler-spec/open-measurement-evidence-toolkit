# Codex for Open Source — Application Package

Prepared from the repository's current public evidence and the official Codex for Open Source application criteria as of August 10, 2026.

## Repository

- GitHub: `ipricetyler-spec/open-measurement-evidence-toolkit`
- Role: Primary maintainer
- License: Apache-2.0
- Status: Public, early-stage pre-release extraction

## Evidence-backed project narrative

Open Measurement Evidence Toolkit is an offline-first .NET library for validating and comparing reproducible Windows performance evidence. It focuses on strict input validation, canonical evidence identity, versioned schemas, PresentMon-compatible CSV analysis, synthetic verification, and conservative repeated-run comparison.

The project deliberately excludes telemetry collection, system tuning, privilege elevation, game injection, anti-cheat interaction, background services, analytics, and network dependencies. That narrow boundary makes the toolkit suitable as reusable infrastructure for developers and technical users who need to reason about performance evidence without coupling analysis to capture or mutation tooling.

The repository includes deterministic maintenance automation, CI, synthetic tests, security and provenance documentation, contribution guidance, governance, dependency monitoring, issue and pull-request templates, third-party notices, and explicit release discipline.

## Current strength and limitation

The strongest evidence today is technical clarity, reproducibility, security boundaries, maintainability, and active repository work. The project is new and does not yet have meaningful public adoption metrics. The application should not imply otherwise.

The official program states that active OSS projects may qualify through meaningful usage, broad adoption, **or clear importance to the software ecosystem**, and considers evidence of active maintenance. This application therefore rests on the project's ecosystem-purpose case plus concrete maintenance evidence, not fabricated stars/downloads/users.

## Form-ready answers

### Describe your role

**Primary maintainer**

I am the primary maintainer and project owner. I define scope and compatibility boundaries, review changes, maintain tests and release automation, handle security/provenance decisions, triage project work, and own release decisions and evidence quality.

### Why does this repository qualify? — max 500 characters

Open Measurement Evidence Toolkit provides offline, reproducible validation and comparison of Windows performance evidence: strict JSON/CSV handling, canonical hashes, versioned schemas, synthetic tests, and conservative repeated-run analysis. It fills a narrow infrastructure gap between raw capture data and trustworthy conclusions. The project is early-stage, so I am applying on ecosystem importance and active maintenance rather than inflated adoption claims.

### Interest

Recommended selections:

- API credits for my project
- Codex Security, if OpenAI considers the repository appropriate for conditional access

### How will you use API credits? — max 500 characters

Use Codex/API credits for real maintainer work: issue triage, PR review assistance, deterministic test generation, schema/compatibility review, release-check automation, documentation consistency checks, and security-focused code review. Automation will remain bounded by repository rules: synthetic fixtures only, no private telemetry, no system mutation, and human-controlled release/security decisions.

### Anything else we should know? — max 500 characters

This project was extracted from a private predecessor under the same owner using a documented allow-list and provenance review. The public repository intentionally contains no private telemetry, signing material, capture binaries, third-party source, or personal evidence. I use a stop-on-first-material-failure release discipline: failed release cycles remain evidence and are never silently rebuilt or re-signed into a pass.

## Submission fields that must come from the account owner

- First name / last name
- Email associated with the ChatGPT account
- OpenAI Organization ID

GitHub username should be `ipricetyler-spec` and the repository URL should point to this public repository.

## Maintainer evidence checklist

- Public Apache-2.0 repository
- Active commit history and ongoing maintenance work
- Deterministic Windows CI and maintenance script
- Synthetic test strategy
- Explicit security/threat boundary
- Provenance and third-party boundary records
- Contribution and conduct guidance
- Maintainer/governance responsibilities
- Dependabot configuration and pinned CI actions
- Issue and pull-request templates
- Release-gate evidence preserving failed-cycle truth

## Application posture

Submit the project as an **early but legitimate maintained OSS infrastructure project**. Do not claim broad adoption, critical-infrastructure status, download volume, or community size that the repository cannot currently demonstrate. The credible case is that reproducible performance evidence is a real developer/technical ecosystem problem, the project addresses a narrow reusable layer, and Codex credits would directly support maintenance, review, testing, release, and security workflows.
