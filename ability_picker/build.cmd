@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
"%CSC%" /nologo /platform:x64 /target:winexe /codepage:65001 /out:"%BIN%\ability_picker.exe" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:"%BIN%\MovieEditorHD.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll ^
  ability_picker\AbilityPicker.cs
if errorlevel 1 goto :fail
mkdir "%BIN%\ability_picker" 2>nul
copy /y ability_picker\data\ability_candidates.json "%BIN%\ability_picker\ability_candidates.json" >nul
echo build OK
goto :eof
:fail
echo BUILD FAILED
exit /b 1
