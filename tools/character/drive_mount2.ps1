# Driven real-input rig v2 for the mount scenarios.
# Fix over v1: verified foreground activation (retry) + PostMessage fallback, so the
# shipped hotkey path (HotkeyTable -> keyCommand -> runCommand) is actually exercised.
param(
    [Parameter(Mandatory = $true)][int]$ProcessId
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RCMountInput2 {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { throw "no main window handle for pid $ProcessId" }
[RCMountInput2]::ShowWindow($h, 9) | Out-Null          # SW_RESTORE
for ($i = 0; $i -lt 20; $i++) {
    [RCMountInput2]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 200
    if ([RCMountInput2]::GetForegroundWindow() -eq $h) { break }
}
$fg = ([RCMountInput2]::GetForegroundWindow() -eq $h)
Write-Output ("activation: foreground=" + $fg)
$WM_KEYDOWN = 0x100; $WM_KEYUP = 0x101
function Key([byte]$vk, [int]$holdMs) {
    if ($script:fgOk) {
        [RCMountInput2]::keybd_event($vk, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds $holdMs
        [RCMountInput2]::keybd_event($vk, 0, 2, [IntPtr]::Zero)
    } else {
        # fallback: direct window messages (same path WinForms receives from the OS)
        [RCMountInput2]::PostMessage($h, $WM_KEYDOWN, [IntPtr]$vk, [IntPtr]0) | Out-Null
        Start-Sleep -Milliseconds $holdMs
        [RCMountInput2]::PostMessage($h, $WM_KEYUP, [IntPtr]$vk, [IntPtr]0) | Out-Null
    }
}
function KeyDown([byte]$vk) {
    if ($script:fgOk) { [RCMountInput2]::keybd_event($vk, 0, 0, [IntPtr]::Zero) }
    else { [RCMountInput2]::PostMessage($h, $WM_KEYDOWN, [IntPtr]$vk, [IntPtr]0) | Out-Null }
}
function KeyUp([byte]$vk) {
    if ($script:fgOk) { [RCMountInput2]::keybd_event($vk, 0, 2, [IntPtr]::Zero) }
    else { [RCMountInput2]::PostMessage($h, $WM_KEYUP, [IntPtr]$vk, [IntPtr]0) | Out-Null }
}
$script:fgOk = $fg
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function At([int]$ms) { while ($sw.ElapsedMilliseconds -lt $ms) { Start-Sleep -Milliseconds 10 } }
$W = 0x57; $S = 0x53; $T = 0x54; $SP = 0x20; $LEFT = 0x25; $RIGHT = 0x27
At 0;     Key $T 80;  Write-Output "t=0     T (mount)"
At 1500;  Key $SP 150; Write-Output "t=1500  Space idle"
At 4000;  KeyDown $W; Write-Output "t=4000  W down"
At 7500;  Key $SP 150; Write-Output "t=7500  Space moving"
At 7800;  Key $SP 150; Write-Output "t=7800  Space airborne double"
At 12000; KeyUp $W;   Write-Output "t=12000 W up"
At 15000; KeyDown $S; Write-Output "t=15000 S down"
At 16500; Key $SP 150; Write-Output "t=16500 Space backward"
At 18500; KeyUp $S;   Write-Output "t=18500 S up"
At 18500; KeyUp $S;   Write-Output "t=18500 S up"
At 19000; KeyDown $RIGHT; Write-Output "t=19000 RIGHT turn"
At 20100; KeyUp $RIGHT;   Write-Output "t=20100 RIGHT turn done"
At 20600; Key $SP 150; Write-Output "t=20600 Space after turn (expect 13618)"
At 21500; Key $T 80;  Write-Output "t=21500 T (dismount)"
At 23500; Write-Output "driver done"
