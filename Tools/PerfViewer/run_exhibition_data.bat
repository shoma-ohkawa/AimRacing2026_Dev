@echo off
rem Collects, unattended and in one go, the data still missing for the exhibition PC (decided 2026/10/27):
rem   1. lap  : 30 laps of base, without "until"      how many plays before the FPS falls (50 FPS is the floor while driving)
rem   2. lap  : 30 laps of maponly, without "until"   the same, with DLSS kept only on the Map camera
rem   3. idle : 120 minutes of base                    how fast memory and VRAM grow while nobody plays
rem "until" is not used on purpose: it would stop each lap run when VRAM reaches 99 percent, before the FPS has a chance to fall.
rem About 2 hours per step, about 6 hours in total (longer if the FPS collapses and the laps slow down; one lap that takes over 900 seconds is abandoned).
rem   run_exhibition_data.bat [lap] [idle] [pc:NAME] [build:DIR] [test] [check]
rem   No arguments (for example, a double click): the plan is shown and the start is confirmed on the console.
rem   lap        : run only steps 1 and 2.       idle : run only step 3.       (neither: all three steps)
rem   pc:NAME    : after the runs, copy the new run folders into PerfRecords\NAME\perf and PerfRecords\NAME\idle (the originals stay where the game wrote them).
rem                NAME is the folder of this PC in PerfRecords, for example Ultra9_5060Ti (the exhibition PC). Without it, nothing is copied.
rem   build:DIR  : build folder (default: next to this file, then ..\..\Build)
rem   test       : a short trial of the same flow (1 lap each of base and maponly, and 3 idle minutes, about 11 minutes). Nothing is copied. The trial folders stay in the game output folder and mix into the totals, so do not use them as records.
rem   check      : only show the plan and the game exe that was found; nothing is started.
rem   examples:
rem     run_exhibition_data.bat check pc:Ultra9_5060Ti
rem     run_exhibition_data.bat test
rem     run_exhibition_data.bat pc:Ultra9_5060Ti
rem Do the trial first. Close Unity Editor and other GPU apps before the real run (the start VRAM and editor_running are recorded in meta.json). Do not touch the keyboard while it runs.
rem This file only calls run_lap_variants.bat and run_idle.bat, so their labels, Player.log copies and summaries are unchanged.
rem Those two end with a pause. Here a blank line is piped into them, so a pause does not stop the chain.
rem A log of this wrapper (start and end times, commands, new run folders) is written to %TEMP%\run_exhibition_data_STAMP.log.
setlocal enabledelayedexpansion

set "HERE=%~dp0"
set "INTERACTIVE="
if "%~1"=="" set "INTERACTIVE=1"
set "DIR="
set "PC="
set "DO_LAP="
set "DO_IDLE="
set "TEST="
set "CHECK="
set "LAPS=30"
set "IDLEMIN=120"
rem productName in ProjectSettings. Same value as in run_lap_variants.bat; change both if the product name changes.
set "PRODUCT=AimRacing2026_Unity6.3"

:parse
if "%~1"=="" goto parsed
set "TOKEN=%~1"
if /I "!TOKEN!"=="lap" set "DO_LAP=1" & goto next
if /I "!TOKEN!"=="idle" set "DO_IDLE=1" & goto next
if /I "!TOKEN!"=="test" set "TEST=1" & goto next
if /I "!TOKEN!"=="check" set "CHECK=1" & goto next
if /I "!TOKEN:~0,3!"=="pc:" set "PC=!TOKEN:~3!" & goto next
if /I "!TOKEN:~0,6!"=="build:" set "DIR=!TOKEN:~6!" & goto next
echo Unknown argument: !TOKEN!
echo Usage: run_exhibition_data.bat [lap] [idle] [pc:NAME] [build:DIR] [test] [check]
exit /b 1
:next
shift
goto parse
:parsed

if not defined DO_LAP if not defined DO_IDLE (
  set "DO_LAP=1"
  set "DO_IDLE=1"
)
if defined TEST (
  set "LAPS=1"
  set "IDLEMIN=3"
)

rem the PC folder name becomes part of a path: letters, digits, underscore, dot and hyphen only, and no ".."
if defined PC (
  echo !PC!| findstr /r "^[A-Za-z0-9_.-][A-Za-z0-9_.-]*$" >nul
  if errorlevel 1 goto badpc
  if not "!PC:..=!"=="!PC!" goto badpc
)
goto pcok
:badpc
echo Invalid pc: name "!PC!". Use letters, digits, underscore, dot and hyphen only, and no "..".
exit /b 1
:pcok

rem find the game exe: build:DIR, else next to this file, else ..\..\Build (the same rule as the two scripts called below)
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
  echo Pass the build folder:  run_exhibition_data.bat build:D:\path\to\Build
  if defined INTERACTIVE pause
  exit /b 1
)
for %%I in ("!DIR!") do set "DIR=%%~fI"
set "BUILDARG="
if defined DIR set BUILDARG="build:!DIR!"

rem destination in PerfRecords (only with pc:NAME, and never in test mode)
set "DEST="
if defined PC if not defined TEST (
  for %%I in ("%HERE%..\..\PerfRecords") do set "PERFROOT=%%~fI"
  if exist "!PERFROOT!\" (
    set "DEST=!PERFROOT!\!PC!"
  ) else (
    echo PerfRecords was not found at "!PERFROOT!". Nothing will be copied.
  )
)

for /f %%T in ('powershell -nologo -noprofile -command "Get-Date -Format yyyyMMdd_HHmmss"') do set "STAMP=%%T"
set "LOG=%TEMP%\run_exhibition_data_%STAMP%.log"

set /a TOTALMIN=0
if defined DO_LAP set /a TOTALMIN+=LAPS*4*2
if defined DO_IDLE set /a TOTALMIN+=IDLEMIN

call :say ""
call :say "==== run_exhibition_data ===="
call :say "Game: !EXE!"
if defined TEST call :say "TEST MODE: short trial, nothing is copied"
if defined DO_LAP call :say "[1] run_lap_variants.bat %LAPS% base x1 build:!DIR!   (about %LAPS% laps x 4 minutes)"
if defined DO_LAP call :say "[2] run_lap_variants.bat %LAPS% maponly x1 build:!DIR!   (about %LAPS% laps x 4 minutes)"
if defined DO_IDLE call :say "[3] run_idle.bat %IDLEMIN% base x1 build:!DIR!   (%IDLEMIN% minutes)"
call :say "About %TOTALMIN% minutes in total, more if the FPS collapses and the laps slow down."
if defined DEST (call :say "After the runs, new run folders are copied to !DEST!") else (call :say "New run folders are not copied (add pc:NAME to copy them into PerfRecords).")
call :say "Log: %LOG%"
tasklist /fi "imagename eq Unity.exe" 2>nul | find /i "Unity.exe" >nul && call :say "WARNING: Unity Editor (Unity.exe) is running. It adds about 3.2 GB of VRAM and makes the records hard to compare. Close it first."
call :say ""

if defined CHECK exit /b 0

if defined INTERACTIVE (
  set "ANS="
  set /p "ANS=Start now? [Y/n]: "
  if /I "!ANS!"=="n" exit /b 0
  if /I "!ANS!"=="no" exit /b 0
)

call :snapshot perf
call :snapshot idle

call :say "[%time%] start"
if defined DO_LAP (
  call :step "[1] lap base" run_lap_variants.bat "%LAPS% base x1"
  call :step "[2] lap maponly" run_lap_variants.bat "%LAPS% maponly x1"
)
if defined DO_IDLE call :step "[3] idle base" run_idle.bat "%IDLEMIN% base x1"
call :say "[%time%] all steps finished"

call :say ""
call :say "New run folders:"
call :collect perf
call :collect idle

if defined PC if not defined TEST (
  call :say ""
  call :say "Summary (run on the project folder):"
  call :say "  python Tools/PerfViewer/lap_report.py --root PerfRecords/!PC!/perf"
  call :say "  python Tools/PerfViewer/lap_report.py --idle --root PerfRecords/!PC!/idle"
)
call :say "Log saved to %LOG%"
if defined INTERACTIVE pause
exit /b 0

:say
rem prints a line and appends it to the log. Do not pass the characters & | < > inside the text.
echo.%~1
>> "%LOG%" echo.%~1
exit /b 0

:step
rem %1 title, %2 script in this folder, %3 its arguments (one quoted string)
call :say "[%time%] %~1 : %~2 %~3"
rem percent expansion on purpose: the two sides of a pipe run in their own cmd.exe, where the delayed expansion of this script is not available
echo.| call "%HERE%%~2" %~3 %BUILDARG%
call :say "[%time%] %~1 : returned"
ping -n 6 127.0.0.1 >nul
exit /b 0

:finddir
rem %1 perf or idle. Sets FOUND to the game's output folder (empty if it does not exist yet)
set "FOUND="
for /d %%C in ("%USERPROFILE%\AppData\LocalLow\*") do (
  if exist "%%C\%PRODUCT%\%~1" set "FOUND=%%C\%PRODUCT%\%~1"
)
exit /b 0

:snapshot
rem %1 perf or idle. Remember the run folders that exist before the runs, to tell the new ones apart afterwards
type nul > "%TEMP%\run_exhibition_data_before_%~1.txt"
call :finddir %~1
if defined FOUND dir /b /ad "!FOUND!" >> "%TEMP%\run_exhibition_data_before_%~1.txt" 2>nul
exit /b 0

:collect
rem %1 perf or idle. Show the stop reason of every new run folder and copy it into PerfRecords when pc:NAME was given
call :finddir %~1
if not defined FOUND (
  call :say "  %~1 : no output folder"
  exit /b 0
)
set "SRC=!FOUND!"
set "NEWCOUNT=0"
for /f "delims=" %%D in ('dir /b /ad "!SRC!" 2^>nul') do (
  findstr /x /c:"%%D" "%TEMP%\run_exhibition_data_before_%~1.txt" >nul 2>nul || call :one %~1 "%%D"
)
call :say "  %~1 : !NEWCOUNT! new run folder(s)"
exit /b 0

:one
rem %1 perf or idle, %2 run folder name
set /a NEWCOUNT+=1
set "S=!SRC!\%~2"
call :say "  %~2"
if exist "!S!\meta.json" (
  for /f "delims=" %%L in ('findstr /c:"stop_reason" /c:"clean_exit" "!S!\meta.json"') do (
    set "LINE=%%L"
    call :showmeta
  )
) else (
  call :say "      meta.json was not found"
)
if not defined DEST exit /b 0
robocopy "!S!" "!DEST!\%~1\%~2" /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  call :say "      COPY FAILED (robocopy exit code 8 or more): !S!"
  exit /b 0
)
set "NS=0"
set "ND=0"
for /f %%A in ('dir /s /b /a-d "!S!" ^| find /c /v ""') do set "NS=%%A"
for /f %%A in ('dir /s /b /a-d "!DEST!\%~1\%~2" ^| find /c /v ""') do set "ND=%%A"
if "!NS!"=="!ND!" (
  call :say "      copied !ND! file(s) to !DEST!\%~1\%~2"
) else (
  call :say "      COPY INCOMPLETE: !NS! file(s) in the source, !ND! in the destination"
)
exit /b 0

:showmeta
rem LINE holds one line of meta.json, like   "stop_reason": "completed",   printed without the quotes and the comma
set "LINE=!LINE:"=!"
set "LINE=!LINE:,=!"
call :say "      !LINE!"
exit /b 0

:findexe
rem first game exe in %DIR% (UnityCrashHandler is skipped)
for %%F in ("%DIR%\*.exe") do (
  if /I not "%%~nxF"=="UnityCrashHandler64.exe" if /I not "%%~nxF"=="UnityCrashHandler32.exe" if not defined EXE set "EXE=%%~fF"
)
exit /b 0
