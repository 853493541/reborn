@echo off
setlocal
set VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat
set OUT=C:\SeasunGame\MovieEditor\bin64\sfx_shim_v6.dll
call "%VCVARS%" >nul
if errorlevel 1 ( echo vcvars64 failed & exit /b 1 )
pushd "%~dp0"
if not exist "%~dp0obj" mkdir "%~dp0obj"
cl /nologo /LD /O2 /W4 /GS- /D_CRT_SECURE_NO_WARNINGS ^
  /Fe:"%OUT%" /Fo:"%~dp0obj\\" sfx_shim.cpp
set RC=%ERRORLEVEL%
popd
if %RC% neq 0 ( echo sfx shim build FAILED & exit /b %RC% )
echo sfx shim build ok -^> %OUT%
exit /b 0
