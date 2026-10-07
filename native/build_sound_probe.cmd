@echo off
setlocal
rem Build sound_probe.dll - read-only Wwise call hooks for 1.6 recon.
rem Loaded only by clients that set RC_SOUND_HOOK=1 (never shared otherwise).
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
set OUT=C:\SeasunGame\MovieEditor\bin64\sound_probe.dll
call "%VCVARS%" >nul
if errorlevel 1 ( echo vcvars64 failed & exit /b 1 )
pushd "%~dp0"
if not exist "%~dp0obj\soundprobe" mkdir "%~dp0obj\soundprobe"
cl /nologo /LD /O2 /W4 /GS- /D_CRT_SECURE_NO_WARNINGS ^
  /Fe:"%OUT%" /Fo:"%~dp0obj\soundprobe\\" sound_probe.cpp ^
  /link /IMPLIB:"%~dp0obj\soundprobe\sound_probe.lib"
set RC=%ERRORLEVEL%
popd
if %RC% neq 0 ( echo sound probe build FAILED & exit /b %RC% )
echo sound probe build ok -^> %OUT%
dumpbin /nologo /exports "%OUT%" | findstr /R "RC_SoundProbe"
if errorlevel 1 ( echo sound probe build FAILED: exports missing & exit /b 1 )
exit /b 0
