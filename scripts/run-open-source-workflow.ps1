param(
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public",
    [string]$MainBranch = "main",
    [switch]$SkipPublish,
    [switch]$SkipBuild,
    [bool]$OpenAfterPublish = $true,
    [bool]$OpenRepoAfterChecks = $false
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "Running open-source workflow bootstrap..."

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $hasWorkingDotnet = $true
    try {
        $dotnetVersion = (& { dotnet --version } 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $dotnetVersion) {
            $hasWorkingDotnet = $false
        }
    } catch {
        $hasWorkingDotnet = $false
    }

    if ($hasWorkingDotnet -and -not $SkipBuild) {
        Write-Host "dotnet available; running full maintenance checks."
        & .\scripts\maintain.ps1
    } else {
        Write-Host "dotnet unavailable or skipped; running boundary + schema checks only."
        & .\scripts\maintain.ps1 -SkipBuild
    }
} else {
    Write-Host "dotnet command not detected; running boundary + schema checks only."
    & .\scripts\maintain.ps1 -SkipBuild
}

if (-not $SkipPublish) {
    Write-Host "Launching publish flow..."
    & .\scripts\publish-one-click.ps1 -Owner $Owner -Repo $Repo -Visibility $Visibility -MainBranch $MainBranch -OpenAfterPublish $OpenAfterPublish
} else {
    Write-Host "Publish skipped by -SkipPublish."
}

if ($OpenRepoAfterChecks) {
    try {
        Start-Process "https://github.com/$Owner/$Repo"
        Write-Host "Opened https://github.com/$Owner/$Repo"
    } catch {
        Write-Warning "Could not open repository page: $($_.Exception.Message)"
    }
}

Write-Host "Open-source workflow complete."
