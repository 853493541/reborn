@echo off
title JX3 Map Selector
REM Map selector / launcher: lists all maps from MovieEditor\ResourcePack\MapList.tab,
REM marks the ones with baked collision data, can bake a map's collision and
REM launches map_spike_host.exe with the right MAP_PATH.
REM
REM Rebuild:
REM   csc /nologo /platform:x64 /target:winexe /out:map_selector.exe ^
REM     /r:System.Windows.Forms.dll /r:System.Drawing.dll MapSelector.cs
"C:\SeasunGame\MovieEditor\bin64\map_selector.exe"
