$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

param(
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public"
)

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $env:GITHUB_TOKEN) {
    Write-Host "No GITHUB_TOKEN detected."
    $token = Read-Host "Paste a GitHub token with repo scope to continue"
    if (-not $token) {
        throw "A GitHub token is required for this action."
    }
    $env:GITHUB_TOKEN = $token
}

.\scripts\publish-to-github.ps1 -Owner $Owner -Repo $Repo -Visibility $Visibility

Write-Host "Done."
Write-Host "Tip: save this token in session scope only (`$env:GITHUB_TOKEN) for future runs."
