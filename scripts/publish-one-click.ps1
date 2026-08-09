param(
    [string]$Owner = "ipricetyler-spec",
    [string]$Repo = "open-measurement-evidence-toolkit",
    [ValidateSet("public", "private")]
    [string]$Visibility = "public",
    [string]$MainBranch = "main",
    [bool]$OpenAfterPublish = $true
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-LocalPublish {
    param([string]$Owner, [string]$Repo, [string]$MainBranch)
    $targetRemote = "https://github.com/$Owner/$Repo.git"
    if (-not (Test-Path ".git")) {
        throw "No local git repo detected and no GITHUB_TOKEN was provided."
    }

    try {
        git remote set-url origin $targetRemote | Out-Null
    } catch {
        git remote add origin $targetRemote
    }

    git push -u origin $MainBranch
    git push --tags
    Write-Host "Pushed local repo to $targetRemote using local git credentials."
}

if (-not $env:GITHUB_TOKEN) {
    Write-Host "No GITHUB_TOKEN detected."
    try {
        Invoke-LocalPublish -Owner $Owner -Repo $Repo -MainBranch $MainBranch
        Write-Host "Done."
        Write-Host "Tip: keep using this for subsequent runs as long as git credentials are valid."
        if ($OpenAfterPublish) {
            try {
                Start-Process "https://github.com/$Owner/$Repo"
                Write-Host "Opened https://github.com/$Owner/$Repo"
            } catch {
                Write-Warning "Publish completed, but could not open browser: $($_.Exception.Message)"
            }
        }
        return
    } catch {
        Write-Host "Local publish path unavailable: $($_.Exception.Message)"
        $token = Read-Host "Paste a GitHub token with repo scope to continue"
        if (-not $token) {
            throw "A GitHub token is required for this action."
        }
        $env:GITHUB_TOKEN = $token
    }
} else {
    Write-Host "Using GITHUB_TOKEN from environment."
}

.\scripts\publish-to-github.ps1 -Owner $Owner -Repo $Repo -Visibility $Visibility

Write-Host "Done."
Write-Host "Tip: save this token in session scope only (`$env:GITHUB_TOKEN) for future runs."
if ($OpenAfterPublish) {
    try {
        Start-Process "https://github.com/$Owner/$Repo"
        Write-Host "Opened https://github.com/$Owner/$Repo"
    } catch {
        Write-Warning "Publish completed, but could not open browser: $($_.Exception.Message)"
    }
}
