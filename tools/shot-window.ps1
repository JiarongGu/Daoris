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
    [string]$ExePath = '',
    # WHICH window of that process. Since SURF8 the shell opens secondary windows — a monitor, a
    # detached session — and `MainWindowHandle` answers for exactly one of them, chosen by Windows
    # rather than by the caller. Without this the polish loop simply cannot see the monitor: the
    # capture silently photographs whichever window the OS calls main. Empty = the main window.
    [string]$WindowTitle = '',
    # WHICH process, by id, when two share an executable and a path cannot tell them apart: a browser
    # a probe started beside the person's own, from the same msedge.exe (2026-09-28).
    [int]$ProcessId = 0
)

Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class DaorisShot {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr state);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int count);
  delegate bool EnumProc(IntPtr hwnd, IntPtr state);

  // Every VISIBLE top-level window a process owns, with its caption. `Process.MainWindowHandle`
  // gives one and never says which, so a process with more than one window is unreachable without
  // this.
  public static List<string> Windows(int pid) {
    var found = new List<string>();
    EnumWindows((hwnd, state) => {
      uint owner;
      GetWindowThreadProcessId(hwnd, out owner);
      if (owner != (uint)pid || !IsWindowVisible(hwnd)) return true;
      var text = new StringBuilder(512);
      GetWindowTextW(hwnd, text, text.Capacity);
      var caption = text.ToString();
      if (caption.Length > 0) found.Add(hwnd.ToInt64() + "|" + caption);
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@ -ReferencedAssemblies System.Runtime.InteropServices, System.Collections
Add-Type -AssemblyName System.Drawing

# PER_MONITOR_AWARE_V2. Without it the capture is the scaled size, so a 1600px window on a 150%
# display is measured and photographed as 1067 — and every pixel reading taken from it is wrong.
[DaorisShot]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null

$candidates = @(Get-Process $ProcessName -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0)
if ($ExePath) { $candidates = @($candidates | Where-Object { $_.Path -eq $ExePath }) }
if ($ProcessId -gt 0) { $candidates = @(Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) }
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

# Which of that process's windows. A title was asked for, so a title that matches nothing is a
# refusal rather than a silent fall back to the main window — the whole point of asking is that the
# main window is not the one wanted.
$handle = $window.MainWindowHandle
if ($WindowTitle) {
    $windows = [DaorisShot]::Windows($window.Id)
    $matched = @($windows | Where-Object { ($_ -split '\|', 2)[1] -like "*$WindowTitle*" })
    if ($matched.Count -eq 0) {
        $captions = ($windows | ForEach-Object { '"' + ($_ -split '\|', 2)[1] + '"' }) -join ', '
        # ASCII only in this sentence: it reaches the caller through a console whose codepage is
        # whatever the machine is set to, and an em dash comes back as mojibake there (measured).
        Write-Error "no window of pid $($window.Id) has a title like '$WindowTitle'. Open: $captions"
        exit 1
    }

    $parts = $matched[0] -split '\|', 2
    $handle = [IntPtr]::new([int64]$parts[0])
    Write-Host "  window: $($parts[1])"
    if ($matched.Count -gt 1) { Write-Host "  $($matched.Count) titles matched; took the first." }
}

$rect = New-Object DaorisShot+RECT
[DaorisShot]::GetWindowRect($handle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

$directory = Split-Path -Parent $OutFile
if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Force $directory | Out-Null }

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[DaorisShot]::PrintWindow($handle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
# Resolve the DIRECTORY and rejoin: Resolve-Path on the file itself fails when it does not exist yet,
# and a relative path here would save beside PowerShell's own location rather than the caller's.
$bitmap.Save((Join-Path (Resolve-Path -Path $directory).Path (Split-Path -Leaf $OutFile)))
$graphics.Dispose()
$bitmap.Dispose()
Write-Output "captured ${width}x${height} -> $OutFile"
