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
set FEATURE=
set SLUG=
if not "%RC_CLIENT_EXE%"=="" set EXE=%RC_CLIENT_EXE%
if not "%RC_CLIENT_EXE%"=="" set FEATURE=1
if not "%RC_CLIENT_EXE%"=="" set SLUG=%RC_CLIENT_EXE:reborn_client_=%
if not "%RC_CLIENT_EXE%"=="" set SLUG=%SLUG:.exe=%
if not "%FEATURE%"=="" if not exist "%BIN%\reborn_%SLUG%" mkdir "%BIN%\reborn_%SLUG%"
"%CSC%" /nologo /unsafe /platform:x64 /target:winexe /out:"%BIN%\%EXE%" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\RebornClient.cs client\JumpTable.cs client\TerrainSampler.cs client\FoliageCollision.cs ^
  client\CameraSystem.cs client\CameraSettings.cs client\EngineRay.cs ^
  client\CameraShim.cs client\VideoSettings.cs
if errorlevel 1 goto :eof
if "%FEATURE%"=="" (
  copy /y client\camera.json "%BIN%\camera.json" >nul
  copy /y client\scene_init_param.txt "%BIN%\scene_init_param.txt" >nul
) else (
  copy /y client\camera.json "%BIN%\reborn_%SLUG%\camera.json" >nul
  copy /y client\scene_init_param.txt "%BIN%\reborn_%SLUG%\scene_init_param.txt" >nul
)
rem build fingerprint for the runtime log
set GIT=unknown
for /f "delims=" %%i in ('git rev-parse --short HEAD 2^>nul') do set GIT=%%i
set DIRTY=0
for /f %%c in ('git status --porcelain ^| find /c /v ""') do set DIRTY=%%c
if "%FEATURE%"=="" (
  (
    echo name=%EXE%
    echo git=%GIT%
    echo dirty=%DIRTY%
    echo built=%DATE% %TIME%
  ) > "%BIN%\build_info.txt"
) else (
  (
    echo name=%EXE%
    echo git=%GIT%
    echo dirty=%DIRTY%
    echo built=%DATE% %TIME%
  ) > "%BIN%\build_info_%EXE%.txt"
)
if "%FEATURE%"=="" (
  "%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\camera_smoke.exe" ^
    client\CameraSystem.cs client\CameraSmoke.cs
) else if not "%RC_SMOKE_EXE%"=="" (
  "%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\%RC_SMOKE_EXE%" ^
    client\CameraSystem.cs client\CameraSmoke.cs
)
echo build exit=%ERRORLEVEL%
