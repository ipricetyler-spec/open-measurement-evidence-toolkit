# Maintainers

The public maintainer identity and contact links will be added when the repository is created. Until then, this local package is not accepting external contributions.

Maintainer responsibilities include issue triage, pull-request review, release management, security response, compatibility decisions, and keeping project claims aligned with evidence.

### Publish bootstrap (local)

After repository identity is confirmed, use the one-click launcher:

```powershell
.\scripts\publish-one-click.bat
```

This will prompt for a PAT once per run if one is not already set in `$env:GITHUB_TOKEN`.

If you'd rather keep it scripted, use:

```powershell
$env:GITHUB_TOKEN = "<your-token>"
.\scripts\publish-one-click.ps1
```

Token should include `repo` scope, and `workflow` if workflow files will be introduced later.
