@echo off
rem Run the sandbox-target-dummy client (one 试炼木桩 at the player spawn).
rem Window title is "targeting" (RC_TITLE override; exe-derived title would be
rem sandbox-target-dummy). Build it first:
rem   set RC_CLIENT_EXE=reborn_client_target-dummy.exe ^&^& client\build_client.cmd
setlocal
set RC_TITLE=targeting
cd /d C:\SeasunGame\MovieEditor
"C:\SeasunGame\MovieEditor\bin64\reborn_client_target-dummy.exe" %*
