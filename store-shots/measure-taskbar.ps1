<#
    Measures the real taskbar and prints the ratios the studio draws with.

    The studio paints its own taskbar rather than pasting a photograph of one,
    so every proportion in StudioSetForm has to come from somewhere. This is
    where. Run it, read the ratios, and put them in the code.

    Everything is reported as a fraction of the band's height, because that is
    the one measurement the studio takes at run time. A taskbar 96 pixels tall
    with 47-pixel icons gives 0.49, and that number holds on a machine whose
    taskbar is a different height.

    Two things make a naive measurement wrong, and both are handled here:

      * The taskbar is translucent, so the wallpaper shows through it and the
        background changes from column to column. Each column is therefore
        compared against its own background, sampled from the band's top edge
        where no icon reaches.

      * The process has to be per-monitor DPI aware, or the capture comes back
        virtualized and every number is halved on a 200% display.

    Usage:  powershell -File store-shots\measure-taskbar.ps1 [-Keep]
            -Keep leaves the captured strip on disk for a look.
#>

param(
    [switch]$Keep
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# ── Capture ──────────────────────────────────────────────────────────────

Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;

public class TaskbarGrab
{
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public static string Capture(string path)
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));   // per-monitor aware v2

        IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
        RECT bounds;
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out bounds))
            return "";

        int width = bounds.Right - bounds.Left;
        int height = bounds.Bottom - bounds.Top;

        using (var strip = new Bitmap(width, height))
        using (var canvas = Graphics.FromImage(strip))
        {
            canvas.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(width, height));
            strip.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }

        return width + "x" + height;
    }
}
"@ -ReferencedAssemblies System.Drawing

$capture = Join-Path $env:TEMP 'studio-taskbar-measure.png'
$size = [TaskbarGrab]::Capture($capture)

if (-not $size) {
    Write-Error 'The taskbar could not be found. Is the shell running?'
    return
}

$band = New-Object System.Drawing.Bitmap $capture
$width = $band.Width
$height = $band.Height

Write-Host "taskbar: $size"
Write-Host ''

# ── Analysis ─────────────────────────────────────────────────────────────

# Group the columns that carry ink into clusters. A run of blank columns this
# wide ends a cluster; narrower gaps sit inside one icon.
function Get-Clusters {
    param([int]$From, [int]$To, [int]$Tolerance = 60, [int]$Separator = 8)

    $clusters = @()
    $start = $null
    $last = 0
    $blank = 0

    for ($x = $From; $x -lt [Math]::Min($To, $width); $x++) {
        $background = $band.GetPixel($x, 2)
        $inked = $false

        for ($y = 6; $y -lt $height - 4; $y++) {
            $pixel = $band.GetPixel($x, $y)
            $distance = [Math]::Abs($pixel.R - $background.R) +
                        [Math]::Abs($pixel.G - $background.G) +
                        [Math]::Abs($pixel.B - $background.B)
            if ($distance -gt $Tolerance) { $inked = $true; break }
        }

        if ($inked) {
            if ($null -eq $start) { $start = $x }
            $last = $x
            $blank = 0
        }
        elseif ($null -ne $start) {
            $blank++
            if ($blank -ge $Separator) {
                $clusters += [pscustomobject]@{ From = $start; To = $last; Width = $last - $start + 1 }
                $start = $null
            }
        }
    }

    if ($null -ne $start) {
        $clusters += [pscustomobject]@{ From = $start; To = $last; Width = $last - $start + 1 }
    }

    return $clusters
}

function Show-Clusters {
    param([string]$Title, $Clusters)

    Write-Host "=== $Title ==="
    foreach ($cluster in $Clusters) {
        $ratio = [Math]::Round($cluster.Width / $height, 2)
        Write-Host ("  x {0,5}..{1,-5} width {2,4}   = {3} of the band" -f `
            $cluster.From, $cluster.To, $cluster.Width, $ratio)
    }

    for ($i = 1; $i -lt $Clusters.Count; $i++) {
        $gap = $Clusters[$i].From - $Clusters[$i - 1].To - 1
        $pitch = $Clusters[$i].From - $Clusters[$i - 1].From
        Write-Host ("     gap {0,4} = {1}    pitch {2,4} = {3}" -f `
            $gap, [Math]::Round($gap / $height, 2), $pitch, [Math]::Round($pitch / $height, 2))
    }
    Write-Host ''
}

# The centered group holds Start, the search box and the pinned icons; the
# notification area holds the hidden-icons chevron, the tray icons and the
# clock. Both are found relative to the middle rather than at fixed offsets.
$middle = [int]($width / 2)
Show-Clusters 'centered group' (Get-Clusters -From ($middle - 500) -To ($middle + 500))
Show-Clusters 'notification area' (Get-Clusters -From ($width - 420) -To $width)

# The darkest pixel in a region is the core of its text, before antialiasing
# lightens the edges.
function Get-DarkestColor {
    param([int]$From, [int]$To)

    $darkest = $null
    $lowest = 999

    for ($x = $From; $x -lt [Math]::Min($To, $width); $x++) {
        for ($y = 6; $y -lt $height - 4; $y++) {
            $pixel = $band.GetPixel($x, $y)
            $luminance = $pixel.R + $pixel.G + $pixel.B
            if ($luminance -lt $lowest) { $lowest = $luminance; $darkest = $pixel }
        }
    }

    return $darkest
}

# How tall the ink stands, which for a CJK label is close to the em size.
function Get-InkHeight {
    param([int]$From, [int]$To, [int]$Below = 120)

    $top = $height
    $bottom = -1

    for ($x = $From; $x -lt [Math]::Min($To, $width); $x++) {
        for ($y = 0; $y -lt $height; $y++) {
            $pixel = $band.GetPixel($x, $y)
            $luminance = 0.299 * $pixel.R + 0.587 * $pixel.G + 0.114 * $pixel.B
            if ($luminance -lt $Below) {
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
        }
    }

    return $bottom - $top + 1
}

Write-Host '=== search box text ==='
$searchFrom = $middle - 380
$searchTo = $middle + 120
$ink = Get-InkHeight -From $searchFrom -To $searchTo
$color = Get-DarkestColor -From $searchFrom -To $searchTo
Write-Host ("  ink height {0,4} = {1} of the band" -f $ink, [Math]::Round($ink / $height, 2))
Write-Host ("  darkest     rgb({0},{1},{2})" -f $color.R, $color.G, $color.B)
Write-Host ''

Write-Host 'Note: the band background is not a color to copy. The taskbar is'
Write-Host 'translucent, so it carries whatever wallpaper is behind it. The'
Write-Host 'studio reads its color from the theme instead.'

$band.Dispose()

if ($Keep) {
    Write-Host ''
    Write-Host "capture kept at $capture"
}
else {
    Remove-Item $capture -Force -ErrorAction SilentlyContinue
}
