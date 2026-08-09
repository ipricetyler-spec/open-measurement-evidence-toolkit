param(
    [string]$RepoPath = "",
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not $RepoPath) {
    $RepoPath = Split-Path -Parent $PSScriptRoot
}

$requiredFiles = @(
    "README.md",
    "LICENSE",
    "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md",
    "SECURITY.md",
    "PROVENANCE.md",
    "ROADMAP.md",
    "docs/maintenance.md",
    "scripts/run-open-source-workflow.ps1",
    "scripts/publish-one-click.ps1",
    "scripts/maintain.ps1"
)

Set-Location $RepoPath

Write-Host "Open-source health snapshot:"
Write-Host "- Repository: https://github.com/$Owner/$Repo"
Write-Host "- Timestamp: $(Get-Date -Format o)"

if (Test-Path ".git") {
    $status = git status --short
    if ($status) {
        Write-Host "WARN: Working tree has uncommitted changes."
        Write-Host $status
    } else {
        Write-Host "PASS: Working tree clean."
    }
} else {
    Write-Error "FAIL: Not a git repository at $RepoPath"
    exit 1
}

foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "PASS: $file"
    } else {
        Write-Error "FAIL: missing $file"
        exit 1
    }
}

if (Test-Path ".github/workflows/ci.yml") {
    Write-Host "PASS: CI workflow present."
} else {
    Write-Error "FAIL: CI workflow missing."
    exit 1
}

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host "PASS: dotnet command available."
} else {
    Write-Host "INFO: dotnet command not available. Full CI/build checks will be limited."
}

Write-Host "PASS: Open-source health check complete."
