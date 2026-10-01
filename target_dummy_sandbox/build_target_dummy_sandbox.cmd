@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
"%CSC%" /nologo /platform:x64 /target:winexe /codepage:65001 /out:"%BIN%\target_dummy_sandbox.exe" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  target_dummy_sandbox\TargetDummySandbox.cs client\TerrainSampler.cs
echo build exit=%ERRORLEVEL%
