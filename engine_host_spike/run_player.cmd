@echo off
title JX3 Map Player - small jumper on real map collision
cd /d C:\SeasunGame\MovieEditor

REM ---- player mode (small actor standing/walking/jumping on real terrain) ----
set MAP_PLAYER=1

REM Controls:
REM   W/S/A/D      walk (always moves the character, camera-relative)
REM   Shift        run
REM   Space        jump
REM   F            toggle FOLLOW (third person) / FREE camera
REM   Alt+right    orbit view angle around the character (follow camera)
REM   right-drag   pan camera
REM   wheel        zoom follow distance (in FOLLOW mode)
REM   free camera: arrow keys move, Q/E up/down
REM
REM Collision: terrain heights come from the game terrain loader; steep
REM slopes/rocks (>~60 degrees) block with wall sliding, ledges >150cm drop you.
REM Structure collision (REAL foliage instances: rocks/cactus/deadwood) is
REM loaded from bin64\collision_data\foliage_collision.bin (regenerate with
REM tools\export_foliage_collision.py). Low rock tops are walkable (step-up).
REM
REM Optional tuning (uncomment to override defaults):
REM set MAP_PLAYER_SCALE=0.5
REM set MAP_PLAYER_SPAWN=147463,0,49911
REM set MAP_PLAYER_JUMP=1100
REM set MAP_PLAYER_GRAVITY=-2500
REM set MAP_PLAYER_SPEED=500
REM set MAP_PLAYER_RUN=1000
REM set MAP_PLAYER_CAM=1        (first person instead of follow camera)
REM set MAP_PLAYER_DEMO=1       (auto walk + jump demo)
REM set MAP_FOLIAGE_COLLISION=0 (disable real rock/cactus collision)
REM set MAP_PLAYER_RADIUS=25    (capsule radius, world units)
REM set MAP_PLAYER_HEIGHT=170   (capsule height, world units)
REM set MAP_PATH=data\source\maps\龙门寻宝\龙门寻宝.jsonmap

"C:\SeasunGame\MovieEditor\bin64\map_spike_host.exe"
