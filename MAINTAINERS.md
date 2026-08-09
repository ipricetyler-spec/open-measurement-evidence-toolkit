# Maintainers

The public maintainer identity and contact links will be added when the repository is created. Until then, this local package is not accepting external contributions.

Maintainer responsibilities include issue triage, pull-request review, release management, security response, compatibility decisions, and keeping project claims aligned with evidence.

### Publish bootstrap (local)

After repository identity is confirmed, run from `public-repo`:

```powershell
$env:GITHUB_TOKEN = "<your-token>"
.\scripts\publish-to-github.ps1 -Owner "<owner>" -Repo "open-measurement-evidence-toolkit" -Visibility "public"
```

The token should include repository create/push and, if available, workflow/visibility management scopes.
