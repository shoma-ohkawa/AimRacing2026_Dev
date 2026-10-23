@echo off
rem Collects the newest recording(s) into one text report (read-only). The report is also saved as perf_result_*.txt
rem next to Player.log (one folder above the perf folder). Paste the report, or copy that .txt file.
rem   collect_result.bat                     the newest recording in the default perf folder
rem   collect_result.bat -Last 3             the newest 3 recordings
rem   collect_result.bat -Root D:\copied\perf
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0collect_result.ps1" %*
echo.
pause
