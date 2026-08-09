[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$forbidden = @(
    'FrameMarq',
    'owner-beta',
    'GuidedPcCalibration',
    'C:\Users\',
    'BEGIN PRIVATE KEY',
    'api_key',
    'access_token'
)

$files = Get-ChildItem -LiteralPath $repositoryRoot -Recurse -Force -File |
    Where-Object {
        $_.FullName -ne $PSCommandPath -and
        $_.FullName -notmatch '[\\/](bin|obj|artifacts|\.git)[\\/]'
    }

$failed = $false
foreach ($pattern in $forbidden) {
    $matches = $files | Select-String -SimpleMatch -CaseSensitive:$false -Pattern $pattern
    if ($matches) {
        Write-Error "Forbidden public-boundary token found: $pattern`n$($matches -join [Environment]::NewLine)"
        $failed = $true
    }
}

if ($failed) { exit 1 }
Write-Output 'PASS: public-boundary scan'
