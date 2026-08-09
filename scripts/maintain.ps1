[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot 'verify-public-boundary.ps1')

Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' } |
    ForEach-Object {
        if ($_.Length -eq 0) { throw "Empty tracked file: $($_.FullName)" }
    }

Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'schemas'),(Join-Path $repositoryRoot 'examples') -Filter '*.json' -File |
    ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json | Out-Null }
Write-Output 'PASS: JSON syntax'

if (-not $SkipBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK is required for build verification. Use -SkipBuild only for boundary and JSON checks.'
    }
    & dotnet build (Join-Path $repositoryRoot 'OpenMeasurementEvidence.sln') --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & dotnet run --project (Join-Path $repositoryRoot 'tests\OpenMeasurementEvidence.Tests\OpenMeasurementEvidence.Tests.csproj') --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Output 'PASS: maintenance checks'
