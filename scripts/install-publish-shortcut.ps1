$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = (Resolve-Path "$PSScriptRoot\..").Path
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Publish Open Measurement Evidence Toolkit.lnk'
$target = Join-Path $root 'scripts\publish-one-click.bat'

if (-not (Test-Path $target)) {
    throw "Expected script not found: $target"
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = $root
$shortcut.Arguments = ""
$shortcut.Description = "One-click publish helper for Open Measurement Evidence Toolkit"
$shortcut.IconLocation = "shell32.dll,131"
$shortcut.Save()

Write-Host "Created shortcut: $shortcutPath"
Write-Host "Target: $target"

$workflowShortcutPath = Join-Path $desktop 'Run Open Source Workflow.lnk'
$workflowTarget = Join-Path $root 'scripts\run-open-source-workflow.bat'
if (-not (Test-Path $workflowTarget)) {
    throw "Expected workflow script not found: $workflowTarget"
}

$workflowShortcut = $shell.CreateShortcut($workflowShortcutPath)
$workflowShortcut.TargetPath = $workflowTarget
$workflowShortcut.WorkingDirectory = $root
$workflowShortcut.Arguments = ""
$workflowShortcut.Description = "Run maintenance checks and publish open-source toolkit"
$workflowShortcut.IconLocation = "shell32.dll,132"
$workflowShortcut.Save()

Write-Host "Created shortcut: $workflowShortcutPath"
Write-Host "Target: $workflowTarget"
