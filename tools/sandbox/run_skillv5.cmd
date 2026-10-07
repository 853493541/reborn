@echo off
rem The map path below is non-ASCII and this file is UTF-8: switch the console
rem codepage before cmd parses it, or the path arrives mojibake (cmd reads
rem batch files in the OEM codepage - 936 here) and LoadMap fails E_FAIL.
chcp 65001 >nul
rem Run the mini sandbox map (cropped loose map) in its own feature client.
rem Build:  set RC_CLIENT_EXE=reborn_client_skillv5.exe   then   client\build_client.cmd
rem Map:    python tools\sandbox\build_sandbox.py --crop 2,2,1,1 --name 龙门寻宝_s
setlocal
cd /d C:\SeasunGame\MovieEditor
set RC_MAP=C:\jx3tmp\reborn_sandbox\map\龙门寻宝_s\龙门寻宝_s.jsonmap
rem Fast startup default for sandbox runs (deviation D7, docs/engine_host/
rem FAST_STARTUP.md): skip the ~21 s editor shader-DB TCP timeout via
rem startup_shim.dll. Requires the exe to be built from a main that includes
rem the RC_STARTUP wiring (reborn_client_skillv5.exe rebuilt at merge). Unset this
rem (or set RC_STARTUP=engine) to run the shipped 24 s path.
set RC_STARTUP=nodb
set RC_TITLE=skill v5
rem Window title comes from the exe name (skill v5; AGENTS.md 2.7).
start "" "C:\SeasunGame\MovieEditor\bin64\reborn_client_skillv5.exe"
