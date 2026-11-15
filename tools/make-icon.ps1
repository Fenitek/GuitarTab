# Genera src/GuitarStudio/Assets/GuitarStudio.ico, l'icona originale dell'applicazione.
# Disegno: corde di una tablatura su un riquadro scuro, con la colonna del cursore
# dell'editor e due note ambra. Colori presi dai valori di Themes/Dark.xaml
# (BgPanel, Line, TextDim, Accent, BgDeep). Nessun elemento grafico di altri programmi.
# Uso: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
param([string]$Output = (Join-Path $PSScriptRoot "..\src\GuitarStudio\Assets\GuitarStudio.ico"))

Add-Type -AssemblyName PresentationCore, WindowsBase
$ErrorActionPreference = "Stop"

function Brush([string]$hex) {
    $b = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
    $b.Freeze(); return $b
}
$panel  = Brush "#26282F"; $border = Brush "#3D404A"; $string = Brush "#B6B9C3"
$accent = Brush "#E8A33D"; $deep   = Brush "#1E1F24"

function Render([int]$size) {
    $v = New-Object System.Windows.Media.DrawingVisual
    $dc = $v.RenderOpen()
    $s = [double]$size
    $u = $s / 32.0                      # disegno su una griglia di 32 unità
    $pad = [Math]::Max(0.5, 1 * $u)
    $rect = New-Object System.Windows.Rect ($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $pen = New-Object System.Windows.Media.Pen ($border, [Math]::Max(1, 1.2 * $u))
    $dc.DrawRoundedRectangle($panel, $pen, $rect, 6.5 * $u, 6.5 * $u)

    # Corde: sei sopra i 24 px, quattro nelle dimensioni piccole per restare leggibili.
    $count = if ($size -le 24) { 4 } else { 6 }
    $top = 8 * $u; $bottom = 24 * $u
    $step = ($bottom - $top) / ($count - 1)
    $thick = [Math]::Max(1, 1.1 * $u)
    $ys = @()
    for ($i = 0; $i -lt $count; $i++) {
        $y = [Math]::Round($top + $i * $step) + 0.5
        $ys += $y
        $p = New-Object System.Windows.Media.Pen ($string, $thick)
        $dc.DrawLine($p, (New-Object System.Windows.Point (5 * $u), $y), (New-Object System.Windows.Point ($s - 5 * $u), $y))
    }

    # Colonna del cursore e due note, come nell'editor.
    $col = New-Object System.Windows.Rect (17 * $u), (5 * $u), (6 * $u), (22 * $u)
    $accentSoft = $accent.Clone(); $accentSoft.Opacity = 0.28
    $dc.DrawRoundedRectangle($accentSoft, $null, $col, 1.5 * $u, 1.5 * $u)
    $r = [Math]::Max(1.6, 3.1 * $u)
    $ring = New-Object System.Windows.Media.Pen ($deep, [Math]::Max(0.8, 1.2 * $u))
    $dc.DrawEllipse($accent, $ring, (New-Object System.Windows.Point (20 * $u), $ys[1]), $r, $r)
    $dc.DrawEllipse($accent, $ring, (New-Object System.Windows.Point (10 * $u), $ys[$count - 2]), $r, $r)
    $dc.Close()

    $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($v)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$images = @(); foreach ($size in $sizes) { $images += ,(Render $size) }

# Formato ICO: intestazione, una voce di 16 byte per immagine, poi i PNG.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($Output), $out.ToArray())
Write-Output ("Scritto {0} ({1} byte, {2} dimensioni)" -f $Output, $out.Length, $sizes.Count)
