# Drive a running reborn client with synthetic player input (camera drags +
# WASD) to reproduce interactive-only engine crashes (D6). Test harness only.
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [int]$Seconds = 240
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RCInput {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public const uint RDOWN = 0x0008, RUP = 0x0010, MDOWN = 0x0002, MUP = 0x0004;
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
$h = $p.MainWindowHandle
[RCInput]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Seconds 2
$r = New-Object RCInput+RECT
[RCInput]::GetWindowRect($h, [ref]$r) | Out-Null
$cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)
$end = (Get-Date).AddSeconds($Seconds)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$angle = 0.0
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
        [RCInput]::SetCursorPos($x, $y)
        Start-Sleep -Milliseconds 12
    }
    [RCInput]::mouse_event([RCInput]::RUP, 0, 0, 0, [IntPtr]::Zero)
    if (($t % 30) -ge 8) { [RCInput]::keybd_event(0x10, 0, 2, [IntPtr]::Zero) }
    [RCInput]::keybd_event(0x57, 0, 2, [IntPtr]::Zero)            # W up
    Start-Sleep -Milliseconds 250
}
if (-not $p.HasExited) { Write-Output "driver done, client alive" }
