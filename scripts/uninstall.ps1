[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$installed = Join-Path $env:LOCALAPPDATA 'Programs\VinceProductivityTray'
$uninstaller = Join-Path $installed 'Uninstall.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) {
    throw "Uninstaller not found at $uninstaller"
}
if ($PSCmdlet.ShouldProcess($installed, 'Launch recoverable per-user uninstall')) {
    Start-Process -FilePath $uninstaller -ArgumentList '--uninstall'
}
