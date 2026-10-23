@echo off
rem Idle monitoring: starts the game with no operation and measures the Opening PV loop (Opening to Splash interlude to Load to Splash to Opening ...).
rem Idle data is kept apart from the lap data: the game writes it to PRODUCT\idle (the lap data goes to PRODUCT\perf).
rem   run_idle.bat [minutes] [conditions...] [xN] [until] [build:DIR]
rem   minutes    : how long to measure (default 120). With "until" it is the upper limit.
rem   conditions : base / nodlss / maponly / unloadoff  (same meaning as run_lap_variants.bat; default when none is named: all four)
rem   xN         : rounds (default x1)
rem   until      : add -perfuntil. A run ends early when VRAM reaches 99% of the GPU memory or the FPS (Opening, median of 10 minutes) falls below 80% of the first 10 minutes.
rem   build:DIR  : build folder (default: next to this file, then ..\..\Build)
rem   examples:
rem     run_idle.bat                  all four conditions, 120 minutes each (about 8 hours)
rem     run_idle.bat 120 base nodlss  baseline and DLSS-off, 120 minutes each
rem     run_idle.bat 120 base         baseline only, 120 minutes
rem     run_idle.bat 480 until        up to 8 hours, stop when VRAM or FPS reaches the limit
rem Do not touch the keyboard while it runs (debug keys are disabled except Esc). Esc quits the current run.
rem Labels: idle_base, idle_nodlss, idle_dlssmap, idle_dlssunload
rem Player.log is copied into each run's folder (idle\LABEL_TIME\Player.log). The summary: lap_report.py --idle --log
rem Output: each run writes meta.json, perf_yyyymmdd.csv and transitions.csv into its own folder under idle\ (see the PRODUCT folder of LocalLow).
rem The rows carry state / lap / trans columns; lap is always 0 here because no lap loop runs. meta.json has mode, stop_reason, start_vram_mb, editor_running.
rem Copy the run folders to PerfRecords\PCNAME\idle\ to keep them apart from the lap data in PerfRecords\PCNAME\perf\.
setlocal enabledelayedexpansion

set "HERE=%~dp0"
set "MINUTES=120"
set "REPEAT=1"
set "UNTIL="
set "DIR="
set "RUN_BASE="
set "RUN_NODLSS="
set "RUN_MAPONLY="
set "RUN_UNLOADOFF="
set "PRODUCT=AimRacing2026_Unity6.3"
set "OUTSUB=idle"
set "PERFDIR="
set "BEFORE=%TEMP%\idle_before.txt"

:parse
if "%~1"=="" goto parsed
set "TOKEN=%~1"
echo !TOKEN!| findstr /r "^[0-9][0-9]*$" >nul && set "MINUTES=!TOKEN!" && goto next
echo !TOKEN!| findstr /r /i "^x[0-9][0-9]*$" >nul && set "REPEAT=!TOKEN:~1!" && goto next
if /I "!TOKEN!"=="until" set "UNTIL=-perfuntil" & goto next
if /I "!TOKEN!"=="base" set "RUN_BASE=1" & goto next
if /I "!TOKEN!"=="nodlss" set "RUN_NODLSS=1" & goto next
if /I "!TOKEN!"=="maponly" set "RUN_MAPONLY=1" & goto next
if /I "!TOKEN!"=="unloadoff" set "RUN_UNLOADOFF=1" & goto next
if /I "!TOKEN:~0,6!"=="build:" set "DIR=!TOKEN:~6!" & goto next
echo Unknown argument: !TOKEN!
echo Usage: run_idle.bat [minutes] [base] [nodlss] [maponly] [unloadoff] [xN] [until] [build:DIR]
exit /b 1
:next
shift
goto parse
:parsed

if "%REPEAT%"=="0" set "REPEAT=1"

rem no condition given = all four
if not defined RUN_BASE if not defined RUN_NODLSS if not defined RUN_MAPONLY if not defined RUN_UNLOADOFF (
  set "RUN_BASE=1"
  set "RUN_NODLSS=1"
  set "RUN_MAPONLY=1"
  set "RUN_UNLOADOFF=1"
)

set "EXE="
if defined DIR (
  call :findexe
) else (
  set "DIR=%HERE%"
  call :findexe
  if not defined EXE (
    set "DIR=%HERE%..\..\Build"
    call :findexe
  )
)
if not defined EXE (
  echo No game exe was found. Looked in: next to this file, then ..\..\Build
  echo Pass the build folder:  run_idle.bat %MINUTES% build:D:\path\to\Build
  pause
  exit /b 1
)

set /a COUNT=0
for %%V in (BASE NODLSS MAPONLY UNLOADOFF) do if defined RUN_%%V set /a COUNT+=1
set /a TOTAL=COUNT*REPEAT
set /a ALLMIN=TOTAL*MINUTES
set /a HOURS=ALLMIN/60

echo Game: "%EXE%"
echo Idle minutes per run: %MINUTES%%UNTIL%
echo Conditions: %COUNT%   Rounds: %REPEAT%   Runs: %TOTAL%   (about %ALLMIN% minutes at most)
echo Close Unity Editor and other GPU apps first (the start VRAM and editor_running are recorded in meta.json).
echo.

for /l %%R in (1,1,%REPEAT%) do (
  echo [!time!] Round %%R of %REPEAT%
  if defined RUN_BASE call :run idle_base ""
  if defined RUN_NODLSS call :run idle_nodlss "-perfnodlss"
  if defined RUN_MAPONLY call :run idle_dlssmap "-perfdlssmaponly"
  if defined RUN_UNLOADOFF call :run idle_dlssunload "-perfdlssoffbeforeunload"
)

echo.
echo All runs finished. Summary:
set "PY="
python --version >nul 2>nul && set "PY=python"
if not defined PY py --version >nul 2>nul && set "PY=py"
if defined PY (
  %PY% "%HERE%lap_report.py" --idle --log
) else (
  echo Python was not found. Install Python 3.7 or later, then run:  python lap_report.py --idle --log
)
pause
exit /b 0

:run
rem %1 label, %2 extra arguments
echo [%time%] %1 : %MINUTES% minutes idle
echo   "%EXE%" -perf -perflabel %1 -perfidle %MINUTES% -perfquit %UNTIL% %~2
call :listruns %1
start "" /wait "%EXE%" -perf -perflabel %1 -perfidle %MINUTES% -perfquit %UNTIL% %~2
call :savelog %1
timeout /t 5 /nobreak >nul 2>nul
exit /b 0

:findperf
set "PERFDIR="
for /d %%C in ("%USERPROFILE%\AppData\LocalLow\*") do (
  if exist "%%C\%PRODUCT%\%OUTSUB%" set "PERFDIR=%%C\%PRODUCT%\%OUTSUB%"
)
exit /b 0

:listruns
type nul > "%BEFORE%"
call :findperf
if defined PERFDIR dir /b /ad "%PERFDIR%\%~1_*" >> "%BEFORE%" 2>nul
exit /b 0

:savelog
call :findperf
if not defined PERFDIR (
  echo   Player.log was not saved: the %OUTSUB% folder was not found.
  exit /b 0
)
set "NEWRUN="
for /f "delims=" %%D in ('dir /b /ad /o-d /t:c "%PERFDIR%\%~1_*" 2^>nul') do (
  if not defined NEWRUN (
    findstr /x /c:"%%D" "%BEFORE%" >nul 2>nul || set "NEWRUN=%%D"
  )
)
if not defined NEWRUN (
  echo   Player.log was not saved: no new run folder was found for %~1.
  exit /b 0
)
for %%I in ("%PERFDIR%\..") do set "GAMEDIR=%%~fI"
if not exist "%GAMEDIR%\Player.log" (
  echo   Player.log was not saved: "%GAMEDIR%\Player.log" was not found.
  exit /b 0
)
copy /y "%GAMEDIR%\Player.log" "%PERFDIR%\%NEWRUN%\Player.log" >nul && echo   saved Player.log to %NEWRUN%
exit /b 0

:findexe
for %%F in ("%DIR%\*.exe") do (
  if /I not "%%~nxF"=="UnityCrashHandler64.exe" if /I not "%%~nxF"=="UnityCrashHandler32.exe" if not defined EXE set "EXE=%%~fF"
)
exit /b 0
