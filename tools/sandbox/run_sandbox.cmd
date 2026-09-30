@echo off
rem Run the mini sandbox map (cropped loose map) in the 大轻功 feature client.
rem Build:  set RC_CLIENT_EXE=reborn_client_daqinggong.exe   then   client\build_client.cmd
rem Map:    python tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name 龙门寻宝_s
setlocal
cd /d C:\SeasunGame\MovieEditor
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
rem Title override per AGENTS.md 2.7 (feature builds otherwise derive sandbox-<slug>).
set RC_TITLE=大轻功
start "" "C:\SeasunGame\MovieEditor\bin64\reborn_client_daqinggong.exe"
