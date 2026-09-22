# Render one MovieEditor map with map_spike_host.exe and copy PNGs to proof.
# Usage: .\engine_host_spike\render_map.ps1 [-Name 龙门寻宝] [-Tour "x,y,z;x,y,z"] [-AutoRunMs 30000] [-NoRun]
param(
    [string]$Name,
    [string]$MapPath,
    [string]$Tour,
    [int]$AutoRunMs = 30000,
    [switch]$NoRun
)
$ErrorActionPreference = "Stop"
$repo = "C:\Users\Zhibin Ren\Desktop\reborn"
$exe = "C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe"
$outDir = "C:\SeasunGame\MovieEditor\bin64\map_spike_out"

if (-not $Name -and -not $MapPath) { throw "pass -Name or -MapPath" }
if (-not $MapPath) { $MapPath = "data\source\maps\$Name\$Name.jsonmap" }
$dest = Join-Path $repo "proof\map_spike\$Name"

$env:MAP_PATH = $MapPath
$env:MAP_AUTORUN = "$AutoRunMs"
if ($Tour) { $env:MAP_TOUR = $Tour } else { Remove-Item Env:MAP_TOUR -ErrorAction SilentlyContinue }

if (-not $NoRun) {
    Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
    & $exe | Out-Null
}
if (Test-Path -LiteralPath $outDir) {
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item -Path (Join-Path $outDir "*.png") -Destination $dest -Force
    Copy-Item -Path (Join-Path $outDir "map.log") -Destination $dest -Force -ErrorAction SilentlyContinue
    Write-Host "copied $(@(Get-ChildItem -LiteralPath $dest -Filter *.png).Count) png -> $dest"
}
