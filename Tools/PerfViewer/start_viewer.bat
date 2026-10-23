@echo off
rem PerfViewer server launcher. Double-click, or run from a console.
rem   start_viewer.bat                       reads the default perf folder and opens the browser
rem   start_viewer.bat --root D:\copied\perf reads another perf folder (for example, one copied from another PC)
rem   start_viewer.bat --port 8800           uses another port
setlocal
cd /d "%~dp0"

set "PY="
python --version >nul 2>nul && set "PY=python"
if not defined PY py --version >nul 2>nul && set "PY=py"
if not defined PY (
  echo Python was not found. Install Python 3.7 or later from https://www.python.org/ and run this file again.
  pause
  exit /b 1
)

%PY% server.py --open %*
if errorlevel 1 pause
