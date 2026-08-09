$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path "$PSScriptRoot\..").Path

function Resolve-ShortcutRoot {
    $candidates = @()
    $desktop = [Environment]::GetFolderPath("Desktop")
    if (-not [string]::IsNullOrWhiteSpace($desktop)) {
        $candidates += $desktop
    }
    if ($env:USERPROFILE) {
        $candidates += Join-Path $env:USERPROFILE "Desktop"
    }
    if ($env:OneDrive) {
        $candidates += Join-Path $env:OneDrive "Desktop"
    }

    $fallback = Join-Path ([System.IO.Path]::GetTempPath()) "CodexOpenSourceToolkitShortcuts"
    $candidates += $fallback

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        try {
            New-Item -ItemType Directory -Force -Path $candidate | Out-Null
            $testFile = Join-Path $candidate ".__codex_shortcut_write_test"
            Set-Content -Path $testFile -Value "ok" -ErrorAction Stop
            Remove-Item -Path $testFile -Force -ErrorAction SilentlyContinue
            return (Resolve-Path $candidate).Path
        } catch {
            continue
        }
    }
}

function New-Shortcut {
    param (
        [Parameter(Mandatory=$true)][string]$Root,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Target,
        [Parameter(Mandatory=$true)][string]$Description,
        [Parameter(Mandatory=$true)][string]$Icon
    )

    $shortcutPath = Join-Path $Root $Name
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $Target
    $shortcut.WorkingDirectory = $root
    $shortcut.Arguments = ""
    $shortcut.Description = $Description
    $shortcut.IconLocation = $Icon
    $shortcut.Save()

    Write-Host "Created shortcut: $shortcutPath"
    Write-Host "Target: $Target"
}

$shortcutRoot = Resolve-ShortcutRoot
if (-not $shortcutRoot) {
    throw "Could not determine a writable shortcut directory."
}

$publishTarget = Join-Path $root 'scripts\publish-one-click.bat'
if (-not (Test-Path $publishTarget)) {
    throw "Expected script not found: $publishTarget"
}
$workflowTarget = Join-Path $root 'scripts\run-open-source-workflow.bat'
if (-not (Test-Path $workflowTarget)) {
    throw "Expected workflow script not found: $workflowTarget"
}
$healthTarget = Join-Path $root 'scripts\open-source-health.bat'
if (-not (Test-Path $healthTarget)) {
    throw "Expected health script not found: $healthTarget"
}

New-Shortcut -Root $shortcutRoot -Name 'Publish Open Measurement Evidence Toolkit.lnk' -Target $publishTarget -Description 'One-click publish helper for Open Measurement Evidence Toolkit' -Icon 'shell32.dll,131'
New-Shortcut -Root $shortcutRoot -Name 'Run Open Source Workflow.lnk' -Target $workflowTarget -Description 'Run maintenance checks and publish open-source toolkit' -Icon 'shell32.dll,132'
New-Shortcut -Root $shortcutRoot -Name 'Open Source Health Check.lnk' -Target $healthTarget -Description 'Run open-source health snapshot for the toolkit' -Icon 'shell32.dll,134'

Write-Host "Installed launchers to: $shortcutRoot"
