@echo off
rem Starts the game build with the -perf option (performance recording).
rem Copy this file into the build folder (next to the game exe) and double-click it, or run it with a path:
rem   run_game_perf.bat [label] [build folder]
rem   run_game_perf.bat baseline
rem   run_game_perf.bat exhibition_before D:\Build\AimRacing
setlocal

set "LABEL=%~1"
if "%LABEL%"=="" set "LABEL=run"
set "DIR=%~2"
if "%DIR%"=="" set "DIR=%~dp0"

set "EXE="
for %%F in ("%DIR%\*.exe") do (
  if /I not "%%~nxF"=="UnityCrashHandler64.exe" if /I not "%%~nxF"=="UnityCrashHandler32.exe" if not defined EXE set "EXE=%%~fF"
)

if not defined EXE (
  echo No game exe was found in "%DIR%".
  echo Copy this file into the build folder, or pass the build folder as the second argument.
  pause
  exit /b 1
)

echo Starting: "%EXE%" -perf -perflabel "%LABEL%"
start "" "%EXE%" -perf -perflabel "%LABEL%"
