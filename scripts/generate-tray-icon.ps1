[CmdletBinding()]
param([string]$Output = (Join-Path $PSScriptRoot '..\src\ChristianSpellHelper\Assets\vince-tray.ico'))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $scale = 4
    $bitmap = [Drawing.Bitmap]::new($size * $scale, $size * $scale, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $fit = [Math]::Min(($size * $scale - 2) / 25.0, ($size * $scale - 2) / 29.0)
        $graphics.TranslateTransform(($size * $scale - 25 * $fit) / 2, ($size * $scale - 29 * $fit) / 2)
        $graphics.ScaleTransform($fit, $fit)
        $path1 = [Drawing.Drawing2D.GraphicsPath]::new()
        $path1.StartFigure(); $path1.AddBezier(1.92902,0.934523,2.99432,0.503616,4.20723,1.01789,4.63814,2.08319)
        $path1.AddLine(4.63814,2.08319,14.0893,25.4486); $path1.AddBezier(14.0893,25.4486,14.5202,26.5139,14.006,27.7269,12.9407,28.1578)
        $path1.AddBezier(12.9407,28.1578,11.8754,28.5887,10.6625,28.0744,10.2315,27.0091); $path1.AddLine(10.2315,27.0091,0.780352,3.64364)
        $path1.AddBezier(0.780352,3.64364,0.349446,2.57834,0.863721,1.36543,1.92902,0.934523); $path1.CloseFigure()
        $path2 = [Drawing.Drawing2D.GraphicsPath]::new()
        $path2.StartFigure(); $path2.AddBezier(23.0875,0.820015,24.1438,1.27269,24.6872,2.36961,24.3013,3.27006)
        $path2.AddLine(24.3013,3.27006,18.594,16.5869); $path2.AddBezier(18.594,16.5869,18.2081,17.4873,17.039,17.8503,15.9828,17.3977)
        $path2.AddBezier(15.9828,17.3977,14.9266,16.945,14.3832,15.8481,14.7691,14.9476); $path2.AddLine(14.7691,14.9476,20.4763,1.63079)
        $path2.AddBezier(20.4763,1.63079,20.8622,0.730339,22.0313,0.367343,23.0875,0.820015); $path2.CloseFigure()
        $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,122,25))
        try { $graphics.FillPath($brush,$path1); $graphics.FillPath($brush,$path2) }
        finally { $brush.Dispose(); $path1.Dispose(); $path2.Dispose() }
    }
    finally { $graphics.Dispose() }
    $final = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [Drawing.Graphics]::FromImage($final)
    try {
        $g2.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g2.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g2.DrawImage($bitmap,0,0,$size,$size)
    } finally { $g2.Dispose(); $bitmap.Dispose() }
    $memory = [IO.MemoryStream]::new()
    try { $final.Save($memory,[Drawing.Imaging.ImageFormat]::Png); return $memory.ToArray() }
    finally { $memory.Dispose(); $final.Dispose() }
}

$sizes = @(16,20,24,32,40,48,64,256)
$frames = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) { $frames.Add((New-Frame $size)) }
$directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Output)); [IO.Directory]::CreateDirectory($directory) | Out-Null
$stream = [IO.File]::Open($Output,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    for ($i=0; $i -lt $frames.Count; $i++) {
        $s=$sizes[$i]; $writer.Write([byte]$(if($s -eq 256){0}else{$s})); $writer.Write([byte]$(if($s -eq 256){0}else{$s}))
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset); $offset += $frames[$i].Length
    }
    foreach($frame in $frames){ $writer.Write($frame) }
} finally { $writer.Dispose(); $stream.Dispose() }
