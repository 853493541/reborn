@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\reborn_client.exe" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\RebornClient.cs client\TerrainSampler.cs client\FoliageCollision.cs ^
  client\CameraSystem.cs
if errorlevel 1 goto :eof
copy /y client\camera.json "%BIN%\camera.json" >nul
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\camera_smoke.exe" ^
  client\CameraSystem.cs client\CameraSmoke.cs
echo build exit=%ERRORLEVEL%
