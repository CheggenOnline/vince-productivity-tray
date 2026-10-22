<#
    opptak.ps1 - tar opp mote, legger WAV-filen i <Utmappe>\audio\ og starter
    transkriber.ps1 i bakgrunnen (lokal whisper, ingen AI-tokens).
    <Utmappe> og resten av oppsettet star i innstillinger.txt.

    Startes med Ctrl+Shift+S via MoteOpptak.ahk. Viser et lite vindu nede til
    hoyre med klokke og stoppknapp. Enter stopper, men bare nar det vinduet har
    fokus, slik at du kan bruke Enter fritt i Teams og Outlook under motet.

    Tar opp mikrofon og systemlyd samtidig og mikser til 16 kHz mono, som er
    formatet whisper vil ha. Begge kilder gar inn med redusert volum slik at
    summen ikke kan overstyre; da slipper vi a klippe hver enkelt sample, som
    ville vaert altfor tregt i PowerShell.

    Merk: UI-en drives med Show() og DoEvents(), ikke Application.Run(). Det er
    med vilje. Register-ObjectEvent-handlerne som tar imot lyd fra lydkortet
    kjores av PowerShell-motoren mellom setninger, og de ville blitt sultet ut
    hvis vi ga kontrollen permanent til WinForms sin egen meldingslokke.
#>

param([switch] $Auto)   # -Auto: startet av MoteOpptak.ahk nar Teams tar mikrofonen, stopper selv nar samtalen slutter

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Skriptet kjorer skjult. Uten denne ville en feil underveis (f.eks. ingen mikrofon)
# bare gjort at ingenting skjedde. Na logges den og vises i en melding.
trap {
    $m = $_.Exception.Message
    try {
        Add-Content -LiteralPath (Join-Path $PSScriptRoot 'moteopptak.log') -Encoding UTF8 `
            -Value ('{0}  [opptak] FEILET: {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $m)
    } catch { }
    [void][Windows.Forms.MessageBox]::Show("Opptaket stoppet med en feil:`n`n$m", 'Moteopptak', 'OK', 'Error')
    exit 1
}

# ------------------------------------------------------------------ oppsett
# Skriptet startes skjult, sa en feil her ville ellers forsvunnet sporlost.
try {
    . (Join-Path $PSScriptRoot 'felles.ps1')
    $cfg = Les-Innstillinger

    # NAudio (lydbiblioteket) ligger i lib\. Faller tilbake til NuGet-cachen.
    foreach ($navn in 'NAudio.Core', 'NAudio.Wasapi') {
        $dll = Join-Path $Root "lib\$navn.dll"
        if (-not (Test-Path -LiteralPath $dll)) {
            $dll = Join-Path $env:USERPROFILE ".nuget\packages\$($navn.ToLower())\2.2.1\lib\netstandard2.0\$navn.dll"
        }
        if (-not (Test-Path -LiteralPath $dll)) { throw "Fant ikke $navn.dll i $Root\lib." }
        Add-Type -Path $dll
    }
} catch {
    [void][Windows.Forms.MessageBox]::Show("Opptaket kunne ikke starte:`n`n$($_.Exception.Message)",
        'Moteopptak', 'OK', 'Error')
    exit 1
}

function TilSample {
    <# ToSampleProvider() er en extension-metode, og PowerShell kan ikke kalle
       slike direkte. Vi velger konverteren selv ut fra formatet. #>
    param($Kilde)
    $f = $Kilde.WaveFormat
    if ($f.Encoding -eq [NAudio.Wave.WaveFormatEncoding]::IeeeFloat) {
        return New-Object NAudio.Wave.SampleProviders.WaveToSampleProvider($Kilde)
    }
    switch ($f.BitsPerSample) {
        8  { return New-Object NAudio.Wave.SampleProviders.Pcm8BitToSampleProvider($Kilde) }
        16 { return New-Object NAudio.Wave.SampleProviders.Pcm16BitToSampleProvider($Kilde) }
        24 { return New-Object NAudio.Wave.SampleProviders.Pcm24BitToSampleProvider($Kilde) }
        32 { return New-Object NAudio.Wave.SampleProviders.Pcm32BitToSampleProvider($Kilde) }
    }
    throw "Ukjent lydformat: $f"
}

# ------------------------------------------------------------------ lyd
$rate = 16000
$mixer = New-Object NAudio.Wave.SampleProviders.MixingSampleProvider(
            [NAudio.Wave.WaveFormat]::CreateIeeeFloatWaveFormat($rate, 1))
$mixer.ReadFully = $true
$opptakere = @()

function Koble {
    param($Kilde, [single] $Styrke)

    $buf = New-Object NAudio.Wave.BufferedWaveProvider($Kilde.WaveFormat)
    $buf.ReadFully = $true
    $buf.DiscardOnBufferOverflow = $true
    $buf.BufferDuration = [TimeSpan]::FromSeconds(20)

    Register-ObjectEvent -InputObject $Kilde -EventName DataAvailable -MessageData $buf -Action {
        if ($EventArgs.BytesRecorded -gt 0) {
            $Event.MessageData.AddSamples($EventArgs.Buffer, 0, $EventArgs.BytesRecorded)
        }
    } | Out-Null

    $sp = TilSample $buf
    if ($sp.WaveFormat.Channels -eq 2) {
        $st = New-Object NAudio.Wave.SampleProviders.StereoToMonoSampleProvider($sp)
        $st.LeftVolume = 0.5; $st.RightVolume = 0.5
        $sp = $st
    } elseif ($sp.WaveFormat.Channels -gt 2) {
        $sp = New-Object NAudio.Wave.SampleProviders.MultiplexingSampleProvider(@($sp), 1)
    }
    if ($sp.WaveFormat.SampleRate -ne $rate) {
        $sp = New-Object NAudio.Wave.SampleProviders.WdlResamplingSampleProvider($sp, $rate)
    }

    $vol = New-Object NAudio.Wave.SampleProviders.VolumeSampleProvider($sp)
    $vol.Volume = $Styrke
    $mixer.AddMixerInput([NAudio.Wave.ISampleProvider] $vol)
}

$enum = New-Object NAudio.CoreAudioApi.MMDeviceEnumerator

$harSystemlyd = $false
try {
    $sys = New-Object NAudio.Wave.WasapiLoopbackCapture
    Koble -Kilde $sys -Styrke 0.7
    $sys.StartRecording()
    $opptakere += $sys
    $harSystemlyd = $true
} catch { }

$micDev = $enum.GetDefaultAudioEndpoint(1, 2)   # Capture, Communications
$mic = New-Object NAudio.CoreAudioApi.WasapiCapture($micDev)
Koble -Kilde $mic -Styrke 0.9
$mic.StartRecording()
$opptakere += $mic

New-Item -ItemType Directory -Path (Join-Path $Root 'tmp') -Force | Out-Null
$wav = Join-Path $Root ('tmp\opptak-{0}.wav' -f (Get-Date -Format 'yyyy-MM-dd-HHmmss'))
# Opptaket skrives i tmp\ og flyttes til <Utmappe>\audio\ forst nar filen er lukket, slik at
# appen som henter derfra aldri ser en halvskrevet WAV.
$OpptakDir = $cfg.Lydmappe
New-Item -ItemType Directory -Path $OpptakDir -Force | Out-Null
$writer = New-Object NAudio.Wave.WaveFileWriter($wav, (New-Object NAudio.Wave.WaveFormat($rate, 16, 1)))

# ------------------------------------------------------------------ vindu
$bg     = [Drawing.Color]::FromArgb(28, 28, 31)
$fg     = [Drawing.Color]::FromArgb(240, 240, 240)
$dempet = [Drawing.Color]::FromArgb(130, 130, 138)
$roed   = [Drawing.Color]::FromArgb(232, 72, 72)

$form = New-Object Windows.Forms.Form
$form.Text = 'Moteopptak'
$form.FormBorderStyle = 'None'
$form.BackColor = $bg
$form.Size = New-Object Drawing.Size(212, 84)
$form.TopMost = $true
$form.ShowInTaskbar = $true
$form.KeyPreview = $true
$form.StartPosition = 'Manual'
$arb = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$form.Location = New-Object Drawing.Point(
    ($arb.Right - $form.Width - 16), ($arb.Bottom - $form.Height - 16))

$prikk = New-Object Windows.Forms.Label
$prikk.Text = [char]0x25CF
$prikk.Font = New-Object Drawing.Font('Segoe UI', 13)
$prikk.ForeColor = $roed
$prikk.AutoSize = $true
$prikk.Location = New-Object Drawing.Point(14, 10)
$form.Controls.Add($prikk)

$klokkeTekst = New-Object Windows.Forms.Label
$klokkeTekst.Text = '00:00:00'
$klokkeTekst.Font = New-Object Drawing.Font('Consolas', 15)
$klokkeTekst.ForeColor = $fg
$klokkeTekst.AutoSize = $true
$klokkeTekst.Location = New-Object Drawing.Point(42, 9)
$form.Controls.Add($klokkeTekst)

$kilde = New-Object Windows.Forms.Label
$kilde.Text = $(if ($harSystemlyd) { 'mikrofon + systemlyd' } else { 'kun mikrofon' })
if ($Auto) { $kilde.Text = 'Teams-auto: ' + $(if ($harSystemlyd) { 'mik + system' } else { 'kun mik' }) }
$kilde.Font = New-Object Drawing.Font('Segoe UI', 7.5)
$kilde.ForeColor = $dempet
$kilde.AutoSize = $true
$kilde.Location = New-Object Drawing.Point(44, 34)
$form.Controls.Add($kilde)

$stopp = New-Object Windows.Forms.Button
$stopp.Text = $(if ($Auto) { 'Stopp' } else { 'Stopp   (Enter)' })
$stopp.Font = New-Object Drawing.Font('Segoe UI', 8.5)
$stopp.FlatStyle = 'Flat'
$stopp.FlatAppearance.BorderSize = 0
$stopp.BackColor = [Drawing.Color]::FromArgb(52, 52, 58)
$stopp.ForeColor = $fg
$stopp.Size = New-Object Drawing.Size(184, 24)
$stopp.Location = New-Object Drawing.Point(14, 52)
$stopp.TabStop = $false
$form.Controls.Add($stopp)

# Flyttbart, siden vinduet ikke har tittellinje
$script:drar = $false
$script:dx = 0; $script:dy = 0
$startDrag = {
    if ($_.Button -eq [Windows.Forms.MouseButtons]::Left) {
        $script:drar = $true
        $script:dx = [Windows.Forms.Cursor]::Position.X - $form.Left
        $script:dy = [Windows.Forms.Cursor]::Position.Y - $form.Top
    }
}
$underDrag = {
    if ($script:drar) {
        $form.Location = New-Object Drawing.Point(
            ([Windows.Forms.Cursor]::Position.X - $script:dx),
            ([Windows.Forms.Cursor]::Position.Y - $script:dy))
    }
}
$sluttDrag = { $script:drar = $false }
foreach ($c in @($form, $prikk, $klokkeTekst, $kilde)) {
    $c.Add_MouseDown($startDrag); $c.Add_MouseMove($underDrag); $c.Add_MouseUp($sluttDrag)
}

$script:skalStoppe = $false
$stopp.Add_Click({ $script:skalStoppe = $true })
# I auto-modus stopper ikke Enter: vinduet dukker opp mens du holder pa i Teams
if (-not $Auto) {
    $form.Add_KeyDown({ if ($_.KeyCode -eq [Windows.Forms.Keys]::Enter) { $script:skalStoppe = $true } })
}
$form.Add_FormClosing({ $script:skalStoppe = $true })

if ($Auto) {
    # Ikke stjel fokus fra Teams nar samtalen starter
    Add-Type -Namespace MoteOpptak -Name Win -MemberDefinition @"
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
"@
    $forrige = [MoteOpptak.Win]::GetForegroundWindow()
    $form.Show()
    [void][MoteOpptak.Win]::SetForegroundWindow($forrige)
} else {
    $form.Show()
    $form.Activate()
}

# ------------------------------------------------------------------ pumpe
$klokke = [Diagnostics.Stopwatch]::StartNew()
$buf = New-Object 'single[]' ($rate / 5)
$skrevet = [long] 0
$sisteSek = -1
$teamsNokkel = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\MSTeams_8wekyb3d8bbwe'
$ledigSek = 0

try {
    while (-not $script:skalStoppe) {
        [Windows.Forms.Application]::DoEvents()

        $skyldig = [long]($klokke.Elapsed.TotalSeconds * $rate) - $skrevet
        if ($skyldig -gt 0) {
            $vil = [Math]::Min($skyldig, $buf.Length)
            $fikk = $mixer.Read($buf, 0, $vil)
            if ($fikk -gt 0) {
                $writer.WriteSamples($buf, 0, $fikk)
                $skrevet += $fikk
            }
        }

        $sek = [int]$klokke.Elapsed.TotalSeconds
        if ($sek -ne $sisteSek) {
            $sisteSek = $sek
            $klokkeTekst.Text = '{0:hh\:mm\:ss}' -f $klokke.Elapsed
            $prikk.Visible = ($sek % 2 -eq 0)     # blinker, sa det er tydelig at det gar
            if ($Auto) {
                # LastUsedTimeStop = 0 betyr at Teams bruker mikrofonen akkurat na
                $tStop = (Get-ItemProperty -LiteralPath $teamsNokkel -Name LastUsedTimeStop -ErrorAction SilentlyContinue).LastUsedTimeStop
                if ($tStop -eq 0) { $ledigSek = 0 } else { $ledigSek++ }
                if ($ledigSek -ge 5) { $script:skalStoppe = $true }   # 5 s slakk mot korte brudd
            }
        }

        Start-Sleep -Milliseconds 20
    }
}
finally {
    $klokke.Stop()
    foreach ($o in $opptakere) { try { $o.StopRecording() } catch { } }
    Get-EventSubscriber | Where-Object { $_.SourceObject -in $opptakere } | Unregister-Event -Force
    try { $writer.Flush() } catch { }
    try { $writer.Dispose() } catch { }
    foreach ($o in $opptakere) { try { $o.Dispose() } catch { } }
}

# ------------------------------------------------------------------ etterpa
$prikk.Visible = $false
$klokkeTekst.ForeColor = $dempet
$stopp.Visible = $false

if ($klokke.Elapsed.TotalSeconds -lt 3) {
    Remove-Item -LiteralPath $wav -Force -ErrorAction SilentlyContinue
    $kilde.Text = 'Under tre sekunder. Forkastet.'
    $kilde.ForeColor = $roed
    $form.Refresh(); [Windows.Forms.Application]::DoEvents()
    Start-Sleep -Seconds 2
    $form.Close()
    return
}

$maal = Join-Path $OpptakDir (Split-Path $wav -Leaf)
try {
    Flytt-Inn -Kilde $wav -Maal $maal
    $kilde.Text = 'Lagret.'
    if ($cfg.Transkriber) {
        $kilde.Text = 'Lagret. Transkriberes i bakgrunnen.'
        # transkriber.ps1 tar alt i audio\ som mangler tekst, ogsa eldre opptak som feilet.
        Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass',
            '-File', ('"{0}"' -f (Join-Path $Root 'transkriber.ps1'))
        )
    }
    $kilde.ForeColor = $dempet
} catch {
    $kilde.Text = 'Kunne ikke flytte. Ligger i tmp.'
    $kilde.ForeColor = $roed
}
$form.Refresh(); [Windows.Forms.Application]::DoEvents()

Start-Sleep -Seconds 2
$form.Close()
