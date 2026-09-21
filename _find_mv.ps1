$paths = @(
  'C:\SeasunGame\jx3-web-map-viewer',
  'C:\Users\Zhibin Ren\jx3-web-map-viewer',
  'D:\jx3-web-map-viewer'
)
foreach ($p in $paths) { if (Test-Path $p) { Write-Output "FOUND $p" } }
Get-ChildItem -Path 'C:\Users\Zhibin Ren' -Filter 'actor-viewer.js' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5 FullName
Get-ChildItem -Path 'C:\SeasunGame' -Filter 'actor-viewer.js' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5 FullName
