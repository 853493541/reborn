@echo off
setlocal
set ROOT=%~dp0..
set TD_DATA=%ROOT%\assets\mode\dummy
cd /d C:\SeasunGame\MovieEditor
"C:\SeasunGame\MovieEditor\bin64\target_dummy_sandbox.exe" %*
