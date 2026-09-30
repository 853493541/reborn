@echo off
rem Run the mini sandbox map (cropped loose map) in its own feature client.
rem Build:  set RC_CLIENT_EXE=reborn_client_mini.exe   then   client\build_client.cmd
rem Map:    python tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name 龙门寻宝_s
setlocal
cd /d C:\SeasunGame\MovieEditor
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
rem Window title comes from the exe name (sandbox-mini; AGENTS.md 2.7).
start "" "C:\SeasunGame\MovieEditor\bin64\reborn_client_mini.exe"
