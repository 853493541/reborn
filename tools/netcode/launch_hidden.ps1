# Launch a command via WMI with the console window hidden (no popup windows on the desktop).
#
# Plain `Invoke-CimMethod Win32_Process Create` pops a console window for every launch
# (user-reported, 2026-10-06). The Win32_ProcessStartup ShowWindow=0 startup info suppresses it.
#
# Usage:
#   powershell -NoProfile -File tools\netcode\launch_hidden.ps1 "cmd /c C:\jx3tmp\run_gamestub_1.cmd"
# (a copy also lives at C:\jx3tmp\launch_hidden.ps1 for quick one-liners)
param([Parameter(Mandatory = $true)][string]$CommandLine)
$si = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]0 }
$r = Invoke-CimMethod Win32_Process -MethodName Create -Arguments @{
    CommandLine = $CommandLine
    ProcessStartupInformation = $si
}
if ($r.ReturnValue -eq 0) {
    Write-Output "launched hidden pid=$($r.ProcessId): $CommandLine"
} else {
    Write-Output "launch FAILED rc=$($r.ReturnValue): $CommandLine"
}
