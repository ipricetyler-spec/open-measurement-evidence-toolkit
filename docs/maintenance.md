# Low-Usage Maintenance

Routine maintenance is deterministic and does not require an AI model:

- `scripts/maintain.ps1` runs the boundary scan, JSON syntax checks, build, and 56 synthetic test scenarios.
- GitHub Actions runs the same check for pushes and pull requests.
- Dependabot checks GitHub Action versions monthly and opens bounded update pull requests.
- CI uses no artifact upload or cache, minimizing retained storage and configuration surface.

Human or model-assisted judgment is reserved for security reports, schema compatibility, architecture changes, ambiguous defects, contributor review, release claims, and application statements. Automated checks reduce work; they do not replace maintainer review or generate artificial activity.
