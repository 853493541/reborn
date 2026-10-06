# capture_window.ps1 - external window screenshots for proofs that must include
# overlays (the engine's own screenshot path renders the 3D scene only).
#
# Usage:
#   powershell -File tools\proof\capture_window.ps1 -ProcessName reborn_client_m1-final `
#       -OutDir C:\SeasunGame\MovieEditor\bin64\reborn_out -Tag hud `
#       -Times 38000,45000,52000 [-Key I] [-KeyDelayMs 500]
#
# Finds the process main window, optionally posts a key press first, then saves
# <OutDir>\capture_<Tag>_<n>_<ms>ms.png at each time (ms since script start).
param(
    [Parameter(Mandatory=$true)][string]$ProcessName,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [Parameter(Mandatory=$true)][string]$Tag,
    [Parameter(Mandatory=$true)][int[]]$Times,
    [string]$Key = "",
    [int]$KeyDelayMs = 800,
    [switch]$AnyClass   # accept any visible top-level window (non-WinForms hosts, e.g. Skill.exe)
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinCap {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
"@
# Physical pixels: without this, GetWindowRect/CopyFromScreen are virtualized on
# HiDPI displays and captures come out downscaled (200% display => half size).
[void][WinCap]::SetProcessDPIAware()

# Pick the largest visible WinForms window of the process (MainWindowHandle can
# return a guard/error dialog instead of the client form).
$targetPids = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
if ($targetPids.Count -eq 0) { Write-Error "no process $ProcessName"; exit 1 }
$script:hwnd = [IntPtr]::Zero
$script:best = 0
$cb = [WinCap+EnumProc]{
    param($h, $l)
    $pid2 = 0
    [void][WinCap]::GetWindowThreadProcessId($h, [ref]$pid2)
    if ($targetPids -contains $pid2 -and [WinCap]::IsWindowVisible($h)) {
        $sb = New-Object System.Text.StringBuilder 256
        [void][WinCap]::GetClassName($h, $sb, 256)
        $cls = $sb.ToString()
        # never pick a console/conhost window: GUI apps launched from a shell
        # may own one, and it is not the render surface (-AnyClass hosts)
        if ($cls -eq "ConsoleWindowClass" -or $cls -eq "CASCADIA_HOSTING_WINDOW_CLASS") {
            return $true
        }
        if ($AnyClass -or $cls.StartsWith("WindowsForms")) {
            $r = New-Object WinCap+RECT
            [void][WinCap]::GetWindowRect($h, [ref]$r)
            $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
            if ($area -gt $script:best) { $script:best = $area; $script:hwnd = $h; $script:hwndPid = $pid2 }
        }
    }
    return $true
}
[void][WinCap]::EnumWindows($cb, [IntPtr]::Zero)
$proc = Get-Process -Id $script:hwndPid
if ($script:hwnd -eq [IntPtr]::Zero) { Write-Error "no visible window for pid(s) $($targetPids -join ',')"; exit 1 }
$hwnd = $script:hwnd
$rect = New-Object WinCap+RECT
[void][WinCap]::GetWindowRect($hwnd, [ref]$rect)
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Output ("window {0} hwnd=0x{1:X} rect=({2},{3})-({4},{5}) {6}x{7}" -f $proc.Id, [int64]$hwnd, $rect.Left, $rect.Top, $rect.Right, $rect.Bottom, $w, $h)
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

if ($Key -ne "") {
    Start-Sleep -Milliseconds $KeyDelayMs
    [void][WinCap]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 200
    $vk = [int][char]$Key.ToUpper()[0]
    [void][WinCap]::PostMessage($hwnd, 0x0100, [IntPtr]$vk, [IntPtr]0)   # WM_KEYDOWN
    Start-Sleep -Milliseconds 60
    [void][WinCap]::PostMessage($hwnd, 0x0101, [IntPtr]$vk, [IntPtr]0)   # WM_KEYUP
    Write-Output ("posted key {0} (vk {1})" -f $Key, $vk)
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$n = 0
foreach ($t in $Times) {
    while ($sw.ElapsedMilliseconds -lt $t) { Start-Sleep -Milliseconds 50 }
    if ($proc.HasExited) { Write-Output ("process exited at {0}ms" -f $sw.ElapsedMilliseconds); break }
    # CopyFromScreen grabs whatever is on top: raise the client first so the
    # capture (and the layered HUD overlay owned by it) is visible.
    [void][WinCap]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 250
    [void][WinCap]::GetWindowRect($hwnd, [ref]$rect)
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ("capture_{0}_{1}_{2}ms.png" -f $Tag, $n, $t)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("saved {0}" -f $path)
    $n++
}
