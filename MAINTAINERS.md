# Maintainers

The public maintainer identity and contact links will be added when the repository is created. Until then, this local package is not accepting external contributions.

Maintainer responsibilities include issue triage, pull-request review, release management, security response, compatibility decisions, and keeping project claims aligned with evidence.

### Publish bootstrap (local)

After repository identity is confirmed, use the one-click launcher:

```powershell
.\scripts\publish-one-click.bat
```

This will prompt for a PAT once per run if one is not already set in `$env:GITHUB_TOKEN`.

Install a desktop shortcut for no-path-run access:

```powershell
.\scripts\install-publish-shortcut.ps1
```

That installer now creates three desktop shortcuts:

- `Publish Open Measurement Evidence Toolkit.lnk`
- `Run Open Source Workflow.lnk`
- `Open Source Health Check.lnk`

The default launcher behavior opens the repository page when finished. Disable for background runs:

```powershell
.\scripts\publish-one-click.ps1 -OpenAfterPublish:$false
```

For full checks plus publish:

```powershell
.\scripts\run-open-source-workflow.ps1
```

This runs `maintain.ps1` and then publish.

```powershell
.\scripts\run-open-source-workflow.ps1 -SkipBuild
```

Use `-SkipBuild` on systems without .NET installed.

Preflight health verification:

```powershell
.\scripts\open-source-health.ps1
```

or

```powershell
.\scripts\open-source-health.bat
```

If you'd rather keep it scripted, use:

```powershell
$env:GITHUB_TOKEN = "<your-token>"
.\scripts\publish-one-click.ps1
```

Token should include `repo` scope, and `workflow` if workflow files will be introduced later.
