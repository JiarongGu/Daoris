# Capture a window into a PNG — the desktop loop's eyes (tools/desktop.mjs shot).
#
# PrintWindow with PW_RENDERFULLCONTENT (flag 2) is what includes the WebView2 composition: the plain
# flag captures the WinForms chrome and leaves the page area blank, which reads as "the app rendered
# nothing" rather than "the capture missed it".
param(
    [Parameter(Mandatory = $true)][string]$OutFile,
    [string]$ProcessName = 'daoris-desktop',
    # WHICH daoris-desktop. There can be two on this machine — the one the owner actually uses and the
    # build from this checkout — and taking whichever Windows lists first photographs the wrong one
    # silently. The sibling this is adapted from lost minutes to exactly that: a change "missing" from
    # a capture of an install that was nineteen commits behind. The caller always passes this.
    [string]$ExePath = ''
)

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class DaorisShot {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@ -ReferencedAssemblies System.Runtime.InteropServices
Add-Type -AssemblyName System.Drawing

# PER_MONITOR_AWARE_V2. Without it the capture is the scaled size, so a 1600px window on a 150%
# display is measured and photographed as 1067 — and every pixel reading taken from it is wrong.
[DaorisShot]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null

$candidates = @(Get-Process $ProcessName -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0)
if ($ExePath) { $candidates = @($candidates | Where-Object { $_.Path -eq $ExePath }) }
$window = $candidates | Select-Object -First 1
if (-not $window) {
    $hint = if ($ExePath) { " from $ExePath" } else { '' }
    Write-Error "no '$ProcessName' window$hint — start one with ``node tools/desktop.mjs run``."
    exit 1
}

# Name what was captured. A reading is only trustworthy if it says where it came from, and with two
# shells running "a Daoris window" is not an answer.
Write-Host "capturing pid $($window.Id) - $($window.Path)"
if ($candidates.Count -gt 1) {
    Write-Host "  $($candidates.Count) matching windows are open; took the first."
}

$rect = New-Object DaorisShot+RECT
[DaorisShot]::GetWindowRect($window.MainWindowHandle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

$directory = Split-Path -Parent $OutFile
if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Force $directory | Out-Null }

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[DaorisShot]::PrintWindow($window.MainWindowHandle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
# Resolve the DIRECTORY and rejoin: Resolve-Path on the file itself fails when it does not exist yet,
# and a relative path here would save beside PowerShell's own location rather than the caller's.
$bitmap.Save((Join-Path (Resolve-Path -Path $directory).Path (Split-Path -Leaf $OutFile)))
$graphics.Dispose()
$bitmap.Dispose()
Write-Output "captured ${width}x${height} -> $OutFile"
