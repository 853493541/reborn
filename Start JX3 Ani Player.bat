@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "PY=.venv\Scripts\python.exe"
if not exist "%PY%" (
    echo [JX3 Ani Player] Missing venv: %PY%
    echo Create it with:
    echo     python -m venv .venv
    echo     .venv\Scripts\python.exe -m pip install -r requirements.txt
    pause
    exit /b 1
)

rem Forward only real flags. Older desktop shortcuts passed a stray "player.py".
set "ARGS="
for %%A in (%*) do (
    set "TOK=%%~A"
    if /I not "!TOK!"=="player.py" set "ARGS=!ARGS! !TOK!"
)

"%PY%" player.py --character-mode fbx !ARGS!
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo.
    echo [JX3 Ani Player] exited with code %RC%. Review the messages above.
    pause
)
exit /b %RC%
