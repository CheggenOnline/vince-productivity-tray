[CmdletBinding()]
param(
    [string]$Version = '0.9.12'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appProject = Join-Path $root 'src\ChristianSpellHelper\ChristianSpellHelper.csproj'
$setupProject = Join-Path $root 'installer\VinceProductivityTray.Setup.csproj'
$releaseRoot = Join-Path $root 'artifacts\release'
$env:NUGET_SCRATCH = Join-Path $root '.nuget-scratch'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'

function Get-PeMachine([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        try {
            $stream.Position = 0x3c
            $peOffset = $reader.ReadInt32()
            $stream.Position = $peOffset + 4
            return $reader.ReadUInt16()
        }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Assert-PeMachine([string]$Path, [uint16]$Expected, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Label is missing: $Path" }
    $actual = Get-PeMachine $Path
    if ($actual -ne $Expected) {
        throw ('{0} has PE machine 0x{1:X4}; expected 0x{2:X4}.' -f $Label, $actual, $Expected)
    }
}

foreach ($rid in @('win-arm64', 'win-x64')) {
    $expectedMachine = if ($rid -eq 'win-arm64') { [uint16]0xAA64 } else { [uint16]0x8664 }
    $appOutput = Join-Path $releaseRoot "app-$Version-$rid"
    $payload = Join-Path $releaseRoot "payload-$Version-$rid.zip"
    $setupOutput = Join-Path $releaseRoot "setup-$Version-$rid"
    dotnet restore $appProject -r $rid --configfile (Join-Path $root 'NuGet.Online.Config') --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw "Application restore failed for $rid." }
    dotnet publish $appProject -c Release -r $rid --self-contained true --no-restore -o $appOutput -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Application publish failed for $rid." }

    $appExecutable = Join-Path $appOutput 'VinceProductivityTray.exe'
    Assert-PeMachine $appExecutable $expectedMachine "$rid application"
    foreach ($relative in @(
        'RecorderAssets\opptak.ps1',
        'RecorderAssets\felles.ps1',
        'RecorderAssets\lib\NAudio.Core.dll',
        'RecorderAssets\lib\NAudio.Wasapi.dll'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $appOutput $relative) -PathType Leaf)) {
            throw "$rid payload is missing $relative."
        }
    }
    $forbidden = @(Get-ChildItem -LiteralPath $appOutput -Recurse -File | Where-Object {
        $_.Extension -eq '.pdb' -or $_.Name -match '^whisper.*\.exe$' -or $_.Name -match '^ggml.*\.bin$'
    })
    if ($forbidden.Count -ne 0) {
        throw "$rid payload contains excluded debug/transcription files: $($forbidden.Name -join ', ')"
    }

    if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Force }
    Compress-Archive -Path (Join-Path $appOutput '*') -DestinationPath $payload -CompressionLevel Optimal

    dotnet restore $setupProject -r $rid --configfile (Join-Path $root 'NuGet.Online.Config') --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw "Installer restore failed for $rid." }
    dotnet publish $setupProject -c Release -r $rid --self-contained true --no-restore -o $setupOutput -p:Version=$Version -p:PayloadZip=$payload -p:DebugType=None -p:DebugSymbols=false -m:1 -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "Installer publish failed for $rid." }

    $publishedSetup = Join-Path $setupOutput 'VinceProductivityTray-Setup.exe'
    Assert-PeMachine $publishedSetup $expectedMachine "$rid installer"

    $asset = Join-Path $releaseRoot "VinceProductivityTray-$Version-$rid-Setup.exe"
    Copy-Item -LiteralPath $publishedSetup -Destination $asset -Force
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $asset).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($asset + '.sha256') -Value "$hash  $([System.IO.Path]::GetFileName($asset))" -Encoding ascii -NoNewline
    [ordered]@{
        version = $Version
        runtime = $rid
        peMachine = ('0x{0:X4}' -f $expectedMachine)
        bytes = (Get-Item -LiteralPath $asset).Length
        sha256 = $hash
        signature = (Get-AuthenticodeSignature -LiteralPath $asset).Status.ToString()
        runtimeExecutionTested = $false
    } | ConvertTo-Json | Set-Content -LiteralPath ($asset + '.manifest.json') -Encoding utf8NoBOM
}
