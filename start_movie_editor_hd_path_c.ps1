# Path C Phase 1 — start MovieEditorHD (corrected cwd)
$me = "C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor"
Start-Process -FilePath "$me\bin64\MovieEditorHD.exe" -ArgumentList "NOTLAUCNER" -WorkingDirectory $me
Write-Host "Started MovieEditorHD NOTLAUCNER cwd=$me"
