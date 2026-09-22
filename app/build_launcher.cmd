@echo off
rem Build the JX3 Reborn launcher (C# net48, x64).
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 ^
  /out:"%~dp0JX3Reborn.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  "%~dp0JX3RebornLauncher.cs"
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo Built: %~dp0JX3Reborn.exe
