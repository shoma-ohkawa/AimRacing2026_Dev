@echo off
rem Runs the automatic laps (10 laps each) for several conditions, one after another, unattended. Each condition is repeated (default 3 rounds).
rem Each run starts the game build with -perf and -perfautolap, quits by itself, and the summary is printed at the end (needs Python).
rem   run_lap_variants.bat [laps] [conditions...] [xN] [until] [build:DIR]
rem   No arguments (for example, a double click): the settings are asked on the console one by one (laps, conditions, rounds, until, build folder).
rem   With arguments: nothing is asked, the arguments are used as they are.
rem   laps       : laps per run (default 10; one lap takes about 4 minutes). With "until" it is the upper limit.
rem   conditions : base      nothing stopped (the baseline)
rem                nodlss    DLSS stopped on every camera
rem                maponly   DLSS kept only on the Map scene camera (stopped on Opening, Menu, Splash, Load, Result cameras)
rem                unloadoff DLSS stays on in every scene, but is stopped on the cameras of the scene being left, when each transition starts (before the scene is discarded)
rem                (default: all four, in this order)
rem   xN         : rounds. Every selected condition is run once per round, round after round (default x3), so the order and the time of day do not favor one condition.
rem   until      : add -perfuntil. A run ends early when VRAM reaches 99 percent of the GPU memory or the FPS falls below 80 percent of lap 1 (the reason is written to meta.json as stop_reason).
rem   build:DIR  : build folder (default: next to this file, then ..\..\Build)
rem   examples:
rem     run_lap_variants.bat                    ask the settings on the console
rem     run_lap_variants.bat 10 maponly x1      only the DLSS-on-Map-only condition, once (about 40 minutes)
rem     run_lap_variants.bat 30 base until x1   baseline, up to 30 laps, stop when VRAM or FPS reaches the limit
rem     run_lap_variants.bat 1 base x1          a quick check: 1 lap, baseline only (about 6 minutes)
rem The plan is printed before the runs start, together with the equivalent command line, so the same run can be repeated without the questions.
rem Do not touch the keyboard while it runs (debug keys are disabled except Esc). Esc quits the current run; the next run then starts.
rem Labels: lap_base, lap_nodlss, lap_dlssmap, lap_dlssunload (the same label for every round; each run has its own folder)
rem Player.log: after each run it is copied into that run's folder (perf\LABEL_TIME\Player.log). Unity keeps only the last two Player.log files,
rem   so without this the [PerfAutoLap] / [AutoDemoLoop] lines of the earlier runs would be lost. (lap_report.py --log reads these copies.)
rem The movie-off conditions (novideo, both) were removed on 2026/10/22: stopping the movie did not change the memory growth. The game still accepts -perfnovideo.
rem Idle (no operation) measurements are separate data: see run_idle.bat.
setlocal enabledelayedexpansion

rem keep this file's folder now: SHIFT below also shifts %0, so %~dp0 would no longer point here
set "HERE=%~dp0"
set "LAPS=10"
set "REPEAT=3"
set "UNTIL="
set "DIR="
set "INTERACTIVE="
set "RUN_BASE="
set "RUN_NODLSS="
set "RUN_MAPONLY="
set "RUN_UNLOADOFF="
rem productName in ProjectSettings. The game writes to %USERPROFILE%\AppData\LocalLow\COMPANY\PRODUCT\perf ; change this if the product name changes.
set "PRODUCT=AimRacing2026_Unity6.3"
set "OUTSUB=perf"
set "PERFDIR="
set "BEFORE=%TEMP%\lap_variants_before.txt"

if "%~1"=="" goto interactive

:parse
if "%~1"=="" goto parsed
set "TOKEN=%~1"
echo !TOKEN!| findstr /r "^[0-9][0-9]*$" >nul && set "LAPS=!TOKEN!" && goto next
echo !TOKEN!| findstr /r /i "^x[0-9][0-9]*$" >nul && set "REPEAT=!TOKEN:~1!" && goto next
if /I "!TOKEN!"=="until" set "UNTIL=-perfuntil" & goto next
if /I "!TOKEN!"=="base" set "RUN_BASE=1" & goto next
if /I "!TOKEN!"=="nodlss" set "RUN_NODLSS=1" & goto next
if /I "!TOKEN!"=="maponly" set "RUN_MAPONLY=1" & goto next
if /I "!TOKEN!"=="unloadoff" set "RUN_UNLOADOFF=1" & goto next
if /I "!TOKEN:~0,6!"=="build:" set "DIR=!TOKEN:~6!" & goto next
echo Unknown argument: !TOKEN!
echo Usage: run_lap_variants.bat [laps] [base] [nodlss] [maponly] [unloadoff] [xN] [until] [build:DIR]
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

rem find the game exe: build:DIR, else next to this file, else ..\..\Build
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
  echo Pass the build folder:  run_lap_variants.bat %LAPS% build:D:\path\to\Build
  pause
  exit /b 1
)

set /a COUNT=0
set "CONDS="
if defined RUN_BASE set /a COUNT+=1 & set "CONDS=!CONDS! base"
if defined RUN_NODLSS set /a COUNT+=1 & set "CONDS=!CONDS! nodlss"
if defined RUN_MAPONLY set /a COUNT+=1 & set "CONDS=!CONDS! maponly"
if defined RUN_UNLOADOFF set /a COUNT+=1 & set "CONDS=!CONDS! unloadoff"
set "UNTILWORD="
if defined UNTIL set "UNTILWORD= until"
set /a TOTAL=COUNT*REPEAT
set /a MINUTES=TOTAL*LAPS*4

echo.
echo Game: "%EXE%"
echo Laps per run: %LAPS%   (about 4 minutes per lap)%UNTILWORD%
echo Conditions: %COUNT%   Rounds: %REPEAT%   Runs: %TOTAL%   (about %MINUTES% minutes at most)
echo Equivalent command: run_lap_variants.bat %LAPS%!CONDS! x%REPEAT%!UNTILWORD!
echo Close Unity Editor and other GPU apps first (the start VRAM and editor_running are recorded in meta.json).
echo.

if defined INTERACTIVE (
  set "ANS="
  set /p "ANS=Start now? [Y/n]: "
  if /I "!ANS!"=="n" exit /b 0
  if /I "!ANS!"=="no" exit /b 0
)

for /l %%R in (1,1,%REPEAT%) do (
  echo [!time!] Round %%R of %REPEAT%
  if defined RUN_BASE call :run lap_base ""
  if defined RUN_NODLSS call :run lap_nodlss "-perfnodlss"
  if defined RUN_MAPONLY call :run lap_dlssmap "-perfdlssmaponly"
  if defined RUN_UNLOADOFF call :run lap_dlssunload "-perfdlssoffbeforeunload"
)

echo.
echo All runs finished. Summary:
set "PY="
python --version >nul 2>nul && set "PY=python"
if not defined PY py --version >nul 2>nul && set "PY=py"
if defined PY (
  %PY% "%HERE%lap_report.py" --log
) else (
  echo Python was not found. Install Python 3.7 or later, then run:  python lap_report.py --log
)
pause
exit /b 0

:interactive
set "INTERACTIVE=1"
echo.
echo ==== run_lap_variants: settings ====
echo Press Enter to keep the value in [ ].
echo.
call :asknum LAPS "Laps per run (about 4 minutes per lap)" %LAPS%
echo.
echo Conditions:
echo   1  base       nothing stopped, the baseline
echo   2  nodlss     DLSS stopped on every camera
echo   3  maponly    DLSS kept only on the Map scene camera
echo   4  unloadoff  DLSS stopped on the cameras of the scene being left
:askconds
set "ANS="
set /p "ANS=Numbers separated by spaces, or all [all]: "
if not defined ANS set "ANS=all"
if /I "!ANS!"=="all" set "ANS=1 2 3 4"
for %%N in (!ANS!) do (
  if "%%N"=="1" set "RUN_BASE=1"
  if "%%N"=="2" set "RUN_NODLSS=1"
  if "%%N"=="3" set "RUN_MAPONLY=1"
  if "%%N"=="4" set "RUN_UNLOADOFF=1"
)
if not defined RUN_BASE if not defined RUN_NODLSS if not defined RUN_MAPONLY if not defined RUN_UNLOADOFF (
  echo   Select at least one number from 1 to 4.
  goto askconds
)
echo.
call :asknum REPEAT "Rounds, every condition is run once per round" %REPEAT%
echo.
set "ANS="
set /p "ANS=Stop a run early when VRAM reaches 99 percent or FPS falls below 80 percent of lap 1 [y/N]: "
if /I "!ANS!"=="y" set "UNTIL=-perfuntil"
if /I "!ANS!"=="yes" set "UNTIL=-perfuntil"
echo.
set "ANS="
set /p "ANS=Build folder [auto-detect]: "
if defined ANS set "DIR=!ANS:"=!"
goto parsed

:asknum
rem %1 variable name, %2 question, %3 current value. Asks for a whole number of 1 or more; Enter keeps the current value.
:asknum_again
set "ANS="
set /p "ANS=%~2 [%~3]: "
if not defined ANS exit /b 0
echo !ANS!| findstr /r "^[0-9][0-9]*$" >nul
if errorlevel 1 (
  echo   Enter a whole number.
  goto asknum_again
)
if "!ANS!"=="0" (
  echo   Enter 1 or more.
  goto asknum_again
)
set "%~1=!ANS!"
exit /b 0

:run
rem %1 label, %2 extra arguments
echo [%time%] %1 : %LAPS% laps
echo   "%EXE%" -perf -perflabel %1 -perfautolap %LAPS% -perfquit %UNTIL% %~2
call :listruns %1
start "" /wait "%EXE%" -perf -perflabel %1 -perfautolap %LAPS% -perfquit %UNTIL% %~2
call :savelog %1
timeout /t 5 /nobreak >nul 2>nul
exit /b 0

:findperf
rem sets PERFDIR to the game's output folder (perf, or idle for run_idle.bat) (empty if there is none yet)
set "PERFDIR="
for /d %%C in ("%USERPROFILE%\AppData\LocalLow\*") do (
  if exist "%%C\%PRODUCT%\%OUTSUB%" set "PERFDIR=%%C\%PRODUCT%\%OUTSUB%"
)
exit /b 0

:listruns
rem %1 label. Remember the run folders that already exist for this label, to tell the new one apart after the run
type nul > "%BEFORE%"
call :findperf
if defined PERFDIR dir /b /ad "%PERFDIR%\%~1_*" >> "%BEFORE%" 2>nul
exit /b 0

:savelog
rem %1 label. Copy Player.log into the run folder that was just created (the newest folder that was not in the list above)
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
rem first game exe in %DIR% (UnityCrashHandler is skipped)
for %%F in ("%DIR%\*.exe") do (
  if /I not "%%~nxF"=="UnityCrashHandler64.exe" if /I not "%%~nxF"=="UnityCrashHandler32.exe" if not defined EXE set "EXE=%%~fF"
)
exit /b 0
