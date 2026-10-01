@echo off
setlocal
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
set OUT=C:\SeasunGame\MovieEditor\bin64\camera_shim.dll
rem The bin64 shim is SHARED by every client: a build from a stale branch (source
rem without the D6 lazy-hash seed, RC_D6Seed) silently reintroduces the D6 AV for all
rem clients (incident 2026-09-30 16:56). Refuse to build/overwrite such sources.
findstr /C:"RC_D6Seed" "%~dp0camera_shim.cpp" >nul
if errorlevel 1 (
  echo shim build REFUSED: camera_shim.cpp has no RC_D6Seed ^(stale branch^).
  echo Merge main into this worktree first - the shared bin64 shim must keep the D6 fix.
  exit /b 1
)
call "%VCVARS%" >nul
if errorlevel 1 ( echo vcvars64 failed & exit /b 1 )
pushd "%~dp0"
if not exist "%~dp0obj" mkdir "%~dp0obj"
cl /nologo /LD /O2 /W4 /GS- /D_CRT_SECURE_NO_WARNINGS ^
  /Fe:"%OUT%" /Fo:"%~dp0obj\\" camera_shim.cpp
set RC=%ERRORLEVEL%
popd
if %RC% neq 0 ( echo shim build FAILED & exit /b %RC% )
echo shim build ok -^> %OUT%
dumpbin /nologo /exports "%OUT%" | findstr /R "RC_Shim"
dumpbin /nologo /exports "%OUT%" | findstr /C:"RC_D6Seed" >nul
if errorlevel 1 ( echo shim build FAILED: RC_D6Seed export missing & exit /b 1 )
exit /b 0
