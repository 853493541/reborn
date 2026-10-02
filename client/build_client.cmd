@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
rem Canonical client (renamed from reborn_camfp.exe, the camera workstream
rem name). Every run logs a fingerprint (name/git/dirty/flags) so its logs
rem are unambiguous.
rem
rem Parallel feature builds (root AGENTS.md §2): set RC_CLIENT_EXE to a unique
rem name (reborn_client_<slug>.exe). That skips the shared bin64 config copies,
rem keeps per-workstream configs in bin64\reborn_<slug>\, and writes
rem build_info_<exe>.txt instead of the shared build_info.txt. Feature builds
rem also skip the smoke exe unless RC_SMOKE_EXE names one.
set EXE=reborn_client.exe
rem Feature workstreams: build a uniquely named client so parallel work cannot
rem overwrite the canonical exe or shared bin64 state (AGENTS.md, parallel
rem client feature work).
if not "%RC_CLIENT_EXE%"=="" set EXE=%RC_CLIENT_EXE%
set SMOKE=camera_smoke.exe
if not "%RC_SMOKE_EXE%"=="" set SMOKE=%RC_SMOKE_EXE%
set BINFO=build_info.txt
if not "%RC_CLIENT_EXE%"=="" set BINFO=build_info_%EXE%.txt
"%CSC%" /nologo /unsafe /platform:x64 /target:winexe /out:"%BIN%\%EXE%" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\RebornClient.cs client\JumpTable.cs client\TerrainSampler.cs client\FoliageCollision.cs ^
  client\CameraSystem.cs client\CameraSettings.cs client\EngineRay.cs ^
  client\CameraShim.cs client\VideoSettings.cs client\Targeting.cs client\UiClient.cs
if errorlevel 1 goto :eof
rem Shared bin64 configs are only written by the canonical build; feature builds
rem must not clobber them while another workstream runs.
if "%RC_CLIENT_EXE%"=="" (
  copy /y client\camera.json "%BIN%\camera.json" >nul
  copy /y client\scene_init_param.txt "%BIN%\scene_init_param.txt" >nul
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
) > "%BIN%\%BINFO%"
rem Canonical builds always build the smoke exe; feature builds only when asked,
rem so a feature never overwrites the shared camera_smoke.exe.
if "%RC_CLIENT_EXE%"=="" goto :smoke
if "%RC_SMOKE_EXE%"=="" goto :done
:smoke
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\%SMOKE%" ^
  client\CameraSystem.cs client\CameraSmoke.cs
:done
echo build exit=%ERRORLEVEL%
