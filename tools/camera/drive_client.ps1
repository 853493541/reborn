# Drive a running reborn client with synthetic player input (camera drags +
# WASD) to reproduce interactive-only engine crashes (D6) and input+camera
# regressions (WW sprint + RMB drag false zoom-in). Test harness only.
#
# Modes:
#   sweep  - W + Shift + RMB circular sweep (D6 crash repro, original)
#   wwdrag - double-tap W (WW sprint) then hold W + RMB yaw sweeps
#            (camera-wwdrag repro: distance must not change while dragging)
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [int]$Seconds = 240,
    [ValidateSet("sweep", "wwdrag")][string]$Mode = "sweep",
    [int]$SweepStepMs = 12,     # wwdrag yaw sweep step time (12 ms = stress flick, 150 ms = human rate)
    [int]$SweepAmp = 220        # wwdrag yaw sweep cursor amplitude (px)
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RCInput {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  public const uint RDOWN = 0x0008, RUP = 0x0010, MDOWN = 0x0002, MUP = 0x0004;
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
$h = $p.MainWindowHandle
# focus the target window (retry: another client window may hold foreground)
for ($i = 0; $i -lt 10; $i++) {
    [RCInput]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 300
    if ([RCInput]::GetForegroundWindow() -eq $h) { break }
}
Start-Sleep -Seconds 1
$r = New-Object RCInput+RECT
[RCInput]::GetWindowRect($h, [ref]$r) | Out-Null
$cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)
# the client's drag lock centers on its panel (client area), not the frame
$cr = New-Object RCInput+RECT
[RCInput]::GetClientRect($h, [ref]$cr) | Out-Null
$pt = New-Object RCInput+POINT
$pt.X = [int](($cr.R - $cr.L) / 2); $pt.Y = [int](($cr.B - $cr.T) / 2)
[RCInput]::ClientToScreen($h, [ref]$pt) | Out-Null
$ccx = $pt.X; $ccy = $pt.Y
$end = (Get-Date).AddSeconds($Seconds)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$angle = 0.0

if ($Mode -eq "wwdrag") {
    # park the cursor on the drag lock center, then WW: W down/up, second W
    # down within 500 ms (the client's double-tap rule)
    [RCInput]::SetCursorPos($ccx, $ccy) | Out-Null
    [RCInput]::keybd_event(0x57, 0, 0, [IntPtr]::Zero)            # W down
    Start-Sleep -Milliseconds 80
    [RCInput]::keybd_event(0x57, 0, 2, [IntPtr]::Zero)            # W up
    Start-Sleep -Milliseconds 60
    [RCInput]::keybd_event(0x57, 0, 0, [IntPtr]::Zero)            # W down (hold -> WW sprint)
    Start-Sleep -Milliseconds 2000                                # sprint pull-back settles
    while ((Get-Date) -lt $end) {
        if ($p.HasExited) { Write-Output "client exited rc=$($p.ExitCode)"; break }
        # small nudge first: the first move past the 4 px dead zone only arms
        # the lock (the client warps the cursor to its center and drops it)
        [RCInput]::SetCursorPos($ccx + 6, $ccy) | Out-Null
        Start-Sleep -Milliseconds 40
        [RCInput]::mouse_event([RCInput]::RDOWN, 0, 0, 0, [IntPtr]::Zero)
        # yaw sweep: horizontal drags around the client center (no pitch change)
        for ($i = 0; $i -lt 60; $i++) {
            $x = [int]($ccx + $SweepAmp * [Math]::Sin($i * 0.16))
            [RCInput]::SetCursorPos($x, $ccy) | Out-Null
            Start-Sleep -Milliseconds $SweepStepMs
        }
        [RCInput]::mouse_event([RCInput]::RUP, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 2500
    }
    [RCInput]::keybd_event(0x57, 0, 2, [IntPtr]::Zero)            # W up
    if (-not $p.HasExited) { Write-Output "driver done, client alive" }
    exit 0
}

while ((Get-Date) -lt $end) {
    if ($p.HasExited) { Write-Output "client exited rc=$($p.ExitCode)"; break }
    $t = $sw.Elapsed.TotalSeconds
    [RCInput]::keybd_event(0x57, 0, 0, [IntPtr]::Zero)            # W down
    if (($t % 30) -lt 8) { [RCInput]::keybd_event(0x10, 0, 0, [IntPtr]::Zero) }  # Shift (sprint)
    [RCInput]::mouse_event([RCInput]::RDOWN, 0, 0, 0, [IntPtr]::Zero)
    for ($i = 0; $i -lt 26; $i++) {
        $angle += 0.35
        # sweep downward and around (pull camera down / look up, per report)
        $x = [int]($cx + 260 * [Math]::Cos($angle))
        $y = [int]($cy + 180 * [Math]::Sin($angle * 0.7))
        [RCInput]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 12
    }
    [RCInput]::mouse_event([RCInput]::RUP, 0, 0, 0, [IntPtr]::Zero)
    if (($t % 30) -ge 8) { [RCInput]::keybd_event(0x10, 0, 2, [IntPtr]::Zero) }
    [RCInput]::keybd_event(0x57, 0, 2, [IntPtr]::Zero)            # W up
    Start-Sleep -Milliseconds 250
}
if (-not $p.HasExited) { Write-Output "driver done, client alive" }
