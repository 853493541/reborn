@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
rem Canonical client (renamed from reborn_camfp.exe, the camera workstream
rem name). Every run logs a fingerprint (name/git/dirty/flags) so its logs
rem are unambiguous.
set EXE=reborn_client.exe
"%CSC%" /nologo /unsafe /platform:x64 /target:winexe /out:"%BIN%\%EXE%" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\RebornClient.cs client\TerrainSampler.cs client\FoliageCollision.cs ^
  client\CameraSystem.cs client\CameraSettings.cs client\EngineRay.cs ^
  client\CameraShim.cs client\VideoSettings.cs
if errorlevel 1 goto :eof
copy /y client\camera.json "%BIN%\camera.json" >nul
copy /y client\scene_init_param.txt "%BIN%\scene_init_param.txt" >nul
rem build fingerprint for the runtime log
set GIT=unknown
for /f "delims=" %%i in ('git rev-parse --short HEAD 2^>nul') do set GIT=%%i
set DIRTY=0
for /f %%c in ('git status --porcelain ^| find /c /v ""') do set DIRTY=%%c
(
  echo name=%EXE%
  echo git=%GIT%
  echo dirty=%DIRTY%
  echo built=%DATE% %TIME%
) > "%BIN%\build_info.txt"
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\camera_smoke.exe" ^
  client\CameraSystem.cs client\CameraSmoke.cs
echo build exit=%ERRORLEVEL%
