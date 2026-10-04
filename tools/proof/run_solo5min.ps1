# run_solo5min.ps1 - M1.7 five-minute solo-run proof driver.
#
# Starts the feature client, drives a continuous solo session with posted keys
# (I = HUD info, G = autorun/run forward, Space = jump, 1 = skill) and saves an
# external window capture every 30 s for 5 minutes. Captures include the layered
# HUD overlay (the engine's own screenshot path renders the 3D scene only).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools\proof\run_solo5min.ps1 `
#       -Exe C:\SeasunGame\MovieEditor\bin64\reborn_client_m1-final.exe `
#       -OutDir C:\SeasunGame\MovieEditor\bin64\reborn_out -Tag solo5
param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [string]$Tag = "solo5",
    [int]$RunMs = 330000
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class SoloCap {
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
[void][SoloCap]::SetProcessDPIAware()

$procName = [System.IO.Path]::GetFileNameWithoutExtension($Exe)
Get-Process -Name $procName -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 500
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$env:RC_DEMO = "0"
$env:RC_AUTORUN = "$RunMs"
$proc = Start-Process $Exe -WorkingDirectory "C:\SeasunGame\MovieEditor" -PassThru
Write-Output ("started {0} pid={1} autorun={2}ms" -f $procName, $proc.Id, $RunMs)

# wait for the main WinForms window
$hwnd = [IntPtr]::Zero
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.ElapsedMilliseconds -lt 60000 -and $hwnd -eq [IntPtr]::Zero) {
    $script:best = 0
    $script:h = [IntPtr]::Zero
    $cb = [SoloCap+EnumProc]{
        param($h, $l)
        $pid2 = 0
        [void][SoloCap]::GetWindowThreadProcessId($h, [ref]$pid2)
        if ($pid2 -eq $proc.Id -and [SoloCap]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [void][SoloCap]::GetClassName($h, $sb, 256)
            if ($sb.ToString().StartsWith("WindowsForms")) {
                $r = New-Object SoloCap+RECT
                [void][SoloCap]::GetWindowRect($h, [ref]$r)
                $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
                if ($area -gt $script:best) { $script:best = $area; $script:h = $h }
            }
        }
        return $true
    }
    [void][SoloCap]::EnumWindows($cb, [IntPtr]::Zero)
    $hwnd = $script:h
    if ($hwnd -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 500 }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Error "no client window"; exit 1 }
Write-Output ("window hwnd=0x{0:X} after {1}ms" -f [int64]$hwnd, $sw.ElapsedMilliseconds)

function Send-Key([int]$vk) {
    # Posted keyboard messages only reach the WinForms KeyDown handler while the
    # client window owns the foreground (same reason the captures raise it).
    [void][SoloCap]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 300
    [void][SoloCap]::PostMessage($hwnd, 0x0100, [IntPtr]$vk, [IntPtr]0)
    Start-Sleep -Milliseconds 80
    [void][SoloCap]::PostMessage($hwnd, 0x0101, [IntPtr]$vk, [IntPtr]0)
}
function Capture([int]$n, [int]$t) {
    [void][SoloCap]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 250
    $r = New-Object SoloCap+RECT
    [void][SoloCap]::GetWindowRect($hwnd, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap(($r.Right - $r.Left), ($r.Bottom - $r.Top))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir ("capture_{0}_{1}_{2}ms.png" -f $Tag, $n, $t)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output ("saved {0}" -f $path)
}

# timeline: init ~29 s, then HUD info + autorun; jumps/skills alternate; captures every 30 s
$nextKey = 60000
$nextCap = 45000
$capIdx = 0
$alt = 0
while ($sw.ElapsedMilliseconds -lt $RunMs + 5000) {
    if ($proc.HasExited) { Write-Output ("client exited at {0}ms" -f $sw.ElapsedMilliseconds); break }
    $t = [int]$sw.ElapsedMilliseconds
    if ($t -ge 32000 -and $t -lt 32300) { Send-Key 0x49; Write-Output "posted I (HUD info)" ; Start-Sleep -Milliseconds 400 }
    elseif ($t -ge 36000 -and $t -lt 36300) {
        # Hold W: KeyDown without KeyUp keeps pW=true (continuous run, 320 u/s).
        [void][SoloCap]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 300
        [void][SoloCap]::PostMessage($hwnd, 0x0100, [IntPtr]0x57, [IntPtr]0)
        Write-Output "posted W down (hold: run)"
        Start-Sleep -Milliseconds 400
    }
    elseif ($t -ge $nextKey) {
        if ($alt % 2 -eq 0) { Send-Key 0x20; Write-Output ("posted Space (jump) at {0}ms" -f $t) }
        else { Send-Key 0x31; Write-Output ("posted 1 (skill) at {0}ms" -f $t) }
        $alt++
        $nextKey = $t + 25000
    }
    if ($t -ge $nextCap) {
        Capture $capIdx $t
        $capIdx++
        $nextCap = $t + 30000
    }
    Start-Sleep -Milliseconds 100
}
# release W so the run ends in a clean idle state
if (-not $proc.HasExited) {
    [void][SoloCap]::PostMessage($hwnd, 0x0101, [IntPtr]0x57, [IntPtr]0)
    Write-Output "posted W up"
}
Write-Output ("done captures={0} exited={1}" -f $capIdx, $proc.HasExited)
