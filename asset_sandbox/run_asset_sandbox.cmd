@echo off
setlocal
set ROOT=%~dp0..
set AS_DATA=%ROOT%\assets\mode\doodad
cd /d C:\SeasunGame\MovieEditor
"C:\SeasunGame\MovieEditor\bin64\asset_sandbox.exe" %*
