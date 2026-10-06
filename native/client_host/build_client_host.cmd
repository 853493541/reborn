@echo off
rem Build the Phase 3 client-engine host core (native/client_host/client_host.cpp).
rem Runtime root: RC_HOST_ROOT (default %TEMP%\opencode\skillv2\client_root).
setlocal
set HERE=%~dp0
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
call "%VCVARS%" >nul
if not exist "%HERE%out" mkdir "%HERE%out"
cl /nologo /O2 /W4 /GS- /utf-8 /Fe:"%HERE%out\client_host.exe" /Fo:"%HERE%out\client_host.obj" "%HERE%client_host.cpp" user32.lib
