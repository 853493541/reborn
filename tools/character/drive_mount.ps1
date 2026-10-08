# Driven real-input repro for the mount (horse) acceptance scenarios.
# Test harness only: sends OS keyboard events to a running reborn client window
# so the shipped hotkey path (HotkeyTable -> keyCommand -> runCommand) is
# exercised, not the RC_MOUNT_TEST scripted shortcuts.
#
# Scenario (times are ms after the driver starts; engine init must already be done):
#    0    T                  -> mount
#  1500  Space              -> idle press (expect: skill 13618, no jump)
#  4000  W down             -> move forward
#  7500  Space              -> moving press (expect: skill 44565 + jump)
#  7800  Space              -> airborne double press (expect: reject, mount kept)
# 12000  W up
# 15000  S down             -> move backward
# 16500  Space              -> backward press (expect: no jump)
# 18500  S up
# 21000  T                  -> dismount
# 23000  end
param(
    [Parameter(Mandatory = $true)][int]$ProcessId
)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RCMountInput {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
$p = Get-Process -Id $ProcessId -ErrorAction Stop
[RCMountInput]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
$sw = [System.Diagnostics.Stopwatch]::StartNew()
function At([int]$ms) { while ($sw.ElapsedMilliseconds -lt $ms) { Start-Sleep -Milliseconds 10 } }
function Key([byte]$vk, [int]$holdMs) {
  [RCMountInput]::keybd_event($vk, 0, 0, [IntPtr]::Zero)
  Start-Sleep -Milliseconds $holdMs
  [RCMountInput]::keybd_event($vk, 0, 2, [IntPtr]::Zero)
}
$W = 0x57; $S = 0x53; $T = 0x54; $SP = 0x20
At 0;    Key $T 80;   Write-Output "t=0    T (mount)"
At 1500; Key $SP 80;  Write-Output "t=1500 Space idle"
At 4000; [RCMountInput]::keybd_event($W, 0, 0, [IntPtr]::Zero); Write-Output "t=4000 W down"
At 7500; Key $SP 80;  Write-Output "t=7500 Space moving"
At 7800; Key $SP 80;  Write-Output "t=7800 Space airborne double"
At 12000; [RCMountInput]::keybd_event($W, 0, 2, [IntPtr]::Zero); Write-Output "t=12000 W up"
At 15000; [RCMountInput]::keybd_event($S, 0, 0, [IntPtr]::Zero); Write-Output "t=15000 S down"
At 16500; Key $SP 80; Write-Output "t=16500 Space backward"
At 18500; [RCMountInput]::keybd_event($S, 0, 2, [IntPtr]::Zero); Write-Output "t=18500 S up"
At 21000; Key $T 80;  Write-Output "t=21000 T (dismount)"
At 23000; Write-Output "driver done"
