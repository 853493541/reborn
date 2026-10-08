@echo off
setlocal
rem Build startup_shim.dll - host override of the shader-DB connect stall
rem (RC_STARTUP=nodb). NOT the shared camera_shim: this DLL is only loaded by
rem clients that set RC_STARTUP (AGENTS.md 2.8 is untouched).
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
set OUT=C:\SeasunGame\MovieEditor\bin64\startup_shim.dll
call "%VCVARS%" >nul
if errorlevel 1 ( echo vcvars64 failed & exit /b 1 )
pushd "%~dp0"
if not exist "%~dp0obj\startup" mkdir "%~dp0obj\startup"
cl /nologo /LD /O2 /W4 /GS- /D_CRT_SECURE_NO_WARNINGS ^
  /Fe:"%OUT%" /Fo:"%~dp0obj\startup\\" startup_shim.cpp ^
  /link /IMPLIB:"%~dp0obj\startup\startup_shim.lib"
set RC=%ERRORLEVEL%
popd
if %RC% neq 0 ( echo startup shim build FAILED & exit /b %RC% )
echo startup shim build ok -^> %OUT%
dumpbin /nologo /exports "%OUT%" | findstr /R "RC_Startup"
if errorlevel 1 ( echo startup shim build FAILED: RC_Startup exports missing & exit /b 1 )
exit /b 0
