@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
"%CSC%" /nologo /platform:x64 /target:exe /out:"%BIN%\actor_map_spike.exe" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:"%BIN%\MovieEditorHD.exe" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  client\ActorMapSpike.cs
echo build exit=%ERRORLEVEL%
