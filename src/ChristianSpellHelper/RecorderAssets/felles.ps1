<#
    felles.ps1 - innstillinger og hjelpefunksjoner for opptak.ps1 og transkriber.ps1.
    Lastes med:  . (Join-Path $PSScriptRoot 'felles.ps1')

    Alt som er spesifikt for en installasjon (hvor filene skal ligge, sprak osv.)
    star i innstillinger.txt i samme mappe. Skriptene selv har ingen faste stier:
    de finner resten av applikasjonen ut fra mappen de ligger i.
#>

$Root             = $PSScriptRoot
$InnstillingerFil = Join-Path $Root 'innstillinger.txt'

function Les-Innstillinger {
    if (-not (Test-Path -LiteralPath $InnstillingerFil)) {
        throw "Fant ikke innstillinger.txt i $Root. Se INSTALLASJON.md."
    }
    $v = @{ Transkriber = 'ja'; Spraak = 'no'; Traader = 'auto' }
    foreach ($linje in Get-Content -LiteralPath $InnstillingerFil -Encoding UTF8) {
        $l = $linje.Trim()
        if ($l -eq '' -or $l.StartsWith('#')) { continue }
        $i = $l.IndexOf('=')
        if ($i -lt 1) { continue }
        $nokkel = $l.Substring(0, $i).Trim()
        $verdi  = $l.Substring($i + 1).Trim().Trim('"')
        $v[$nokkel] = [Environment]::ExpandEnvironmentVariables($verdi)
    }
    if (-not $v['Utmappe']) { throw "innstillinger.txt mangler linjen 'Utmappe = ...'." }
    $ut = $v['Utmappe'].TrimEnd('\')
    $traader = [Math]::Min(8, [Environment]::ProcessorCount)
    if ($v['Traader'] -match '^\s*\d+\s*$') { $traader = [int]$v['Traader'] }
    [pscustomobject]@{
        Utmappe     = $ut
        Lydmappe    = Join-Path $ut 'audio'
        Transkriber = ($v['Transkriber'] -match '^\s*(ja|yes|true|1)\s*$')
        Spraak      = $v['Spraak']
        Traader     = $traader
    }
}

function Flytt-Inn {
    # Flytter en ferdig fil inn i en mappe som andre leser fra, uten at de noen gang
    # ser en halvskrevet fil: forst til <navn>.part i malmappen (en kopi hvis malet
    # ligger pa en annen disk), deretter et rent navnebytte, som alltid er atomisk.
    param([string] $Kilde, [string] $Maal)
    $part = "$Maal.part"
    Move-Item -LiteralPath $Kilde -Destination $part -Force
    Move-Item -LiteralPath $part  -Destination $Maal
}

function Ascii-Sti {
    # whisper-cli.exe krasjer (kode -1073740791) hvis stien til programmet, modellen
    # eller lydfila inneholder tegn som æøå - f.eks. brukernavnet TorjusTjønn.
    # Vi gir den derfor Windows' korte 8.3-navn (C:\Users\TORJUS~1\...), som bare
    # har vanlige tegn. Stien ma finnes. Returnerer stien uendret hvis den er ren.
    param([string] $Sti)
    if ($Sti -notmatch '[^\x00-\x7F]') { return $Sti }
    $fso = New-Object -ComObject Scripting.FileSystemObject
    $kort = if (Test-Path -LiteralPath $Sti -PathType Container) { $fso.GetFolder($Sti).ShortPath } else { $fso.GetFile($Sti).ShortPath }
    if ($kort -match '[^\x00-\x7F]') {
        throw "Stien inneholder æ, ø eller å, og Windows har ikke korte navn pa denne disken: $Sti. Installer Moteopptak i en mappe uten slike tegn, f.eks. C:\MoteOpptak (kjor INSTALLER.cmd pa nytt)."
    }
    return $kort
}
