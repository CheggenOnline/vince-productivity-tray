[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = Join-Path $projectRoot 'artifacts\release'
$distributionRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'distribution'))
if ($distributionRoot -ne [IO.Path]::Combine($projectRoot, 'distribution')) {
    throw 'Distribution destination is outside the expected project folder.'
}
if (-not (Test-Path -LiteralPath (Join-Path $distributionRoot 'README.md'))) {
    throw 'Distribution README is missing.'
}

# Validate both installers before touching the current sharing folder. No setup is run.
$assets = foreach ($runtime in @('win-arm64', 'win-x64')) {
    $name = "VinceProductivityTray-$Version-$runtime-Setup.exe"
    $source = Join-Path $releaseRoot $name
    $expected = ((Get-Content -LiteralPath ($source + '.sha256') -Raw).Trim() -split '\s+')[0]
    $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($expected -notmatch '^[0-9a-fA-F]{64}$' -or $actual -ne $expected) {
        throw "Installer checksum failed: $name"
    }
    [pscustomobject]@{ Name = $name; Source = $source; Hash = $actual }
}

foreach ($asset in $assets) {
    $destination = Join-Path $distributionRoot $asset.Name
    Copy-Item -LiteralPath $asset.Source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $asset.Hash) {
        throw "Copied installer checksum failed: $($asset.Name)"
    }
}

# Remove only obsolete generated installer files, never user files or directories.
Get-ChildItem -LiteralPath $distributionRoot -File | Where-Object {
    $_.Name -match '^VinceProductivityTray-\d+\.\d+\.\d+-win-(arm64|x64)-Setup\.exe$' -and
    $_.Name -notin $assets.Name
} | ForEach-Object { Remove-Item -LiteralPath $_.FullName }

Write-Output "Prepared distribution for $Version (ARM64 and x64). Nothing installed or uploaded."
