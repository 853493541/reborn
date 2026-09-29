@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
rem Canonical client (renamed from reborn_camfp.exe, the camera workstream
rem name). Every run logs a fingerprint (name/git/dirty/flags) so its logs
rem are unambiguous.
rem Feature builds (AGENTS.md §2): set RC_CLIENT_EXE=reborn_client_<slug>.exe
rem to avoid clobbering the canonical exe; shared config copies and
rem build_info.txt are skipped, build_info_<exe>.txt is written instead.
rem The smoke build is skipped unless RC_SMOKE_EXE is set.
set EXE=reborn_client.exe
if not "%RC_CLIENT_EXE%"=="" set EXE=%RC_CLIENT_EXE%
"%CSC%" /nologo /unsafe /platform:x64 /target:winexe /out:"%BIN%\%EXE%" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\RebornClient.cs client\TerrainSampler.cs client\FoliageCollision.cs ^
  client\CameraSystem.cs client\CameraSettings.cs client\EngineRay.cs ^
  client\CameraShim.cs client\VideoSettings.cs
if errorlevel 1 goto :eof
if "%RC_CLIENT_EXE%"=="" (
  copy /y client\camera.json "%BIN%\camera.json" >nul
  copy /y client\scene_init_param.txt "%BIN%\scene_init_param.txt" >nul
  set BINFO=%BIN%\build_info.txt
) else (
  set BINFO=%BIN%\build_info_%EXE%.txt
)
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
) > "%BINFO%"
if "%RC_CLIENT_EXE%"=="" goto :smoke
if "%RC_SMOKE_EXE%"=="" goto :done
:smoke
set SMOKE=camera_smoke.exe
if not "%RC_SMOKE_EXE%"=="" set SMOKE=%RC_SMOKE_EXE%
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\%SMOKE%" ^
  client\CameraSystem.cs client\CameraSmoke.cs
:done
echo build exit=%ERRORLEVEL%
