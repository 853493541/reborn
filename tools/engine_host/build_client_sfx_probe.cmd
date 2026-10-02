@echo off
rem Build the client-stack SFX recon probe (see docs/engine_host/SFX_WIRING_PLAN.md).
rem Runtime root: set RC_PROBE_ROOT to a client-root copy
rem (default: %TEMP%\opencode\skillv2\client_root).
setlocal
set HERE=%~dp0
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
call "%VCVARS%" >nul
if not exist "%HERE%out" mkdir "%HERE%out"
cl /nologo /O2 /W4 /GS- /utf-8 /Fe:"%HERE%out\client_sfx_probe.exe" /Fo:"%HERE%out\client_sfx_probe.obj" "%HERE%client_sfx_probe.cpp" user32.lib
