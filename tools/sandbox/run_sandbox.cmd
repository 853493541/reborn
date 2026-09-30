@echo off
rem Run the mini sandbox map (cropped loose map) in its own feature client.
rem Build:  set RC_CLIENT_EXE=reborn_client_pure.exe   then   client\build_client.cmd
rem Map:    python tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name ÁúÃÅÑ°±¦_s
setlocal
cd /d C:\SeasunGame\MovieEditor
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\ÁúÃÅÑ°±¦_s\ÁúÃÅÑ°±¦_s.jsonmap
set RC_TITLE=Sandbox-pure
start "" "C:\SeasunGame\MovieEditor\bin64\reborn_client_pure.exe"
