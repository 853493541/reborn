@echo off
rem Run the ability sandbox (Skill.exe) on the cropped mini sandbox map.
rem Build:  ability_sandbox\build.cmd
rem Map:    python tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name 龙门寻宝_s
setlocal
cd /d C:\SeasunGame\MovieEditor
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
rem Window title comes from the app (sandbox-ability; AGENTS.md 2.7).
start "" "C:\SeasunGame\MovieEditor\bin64\Skill.exe"
