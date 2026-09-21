@echo off
cd /d "%~dp0"
REM Product default: ?? FBX character view. Stick/mesh is debug only (--character-mode mesh).
".venv\Scripts\python.exe" player.py --character-mode fbx %*
