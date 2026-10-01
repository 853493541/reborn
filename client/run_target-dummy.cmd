@echo off
rem Run the sandbox-target-dummy client (one 试炼木桩 at the player spawn).
rem Window title is "targeting" (RC_TITLE override; exe-derived title would be
rem sandbox-target-dummy). Build it first:
rem   set RC_CLIENT_EXE=reborn_client_target-dummy.exe ^&^& client\build_client.cmd
rem RC_UI_ROOT points the target HUD at the client UI extracted by
rem   python tools\netcode\ui\extract_target_frame.py
setlocal
set RC_TITLE=targeting
set ROOT=%~dp0..
set RC_UI_ROOT=%ROOT%\assets\ui\targetframe
cd /d C:\SeasunGame\MovieEditor
"C:\SeasunGame\MovieEditor\bin64\reborn_client_target-dummy.exe" %*
