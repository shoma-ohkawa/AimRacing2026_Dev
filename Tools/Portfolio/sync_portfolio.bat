@echo off
rem Writes the portfolio files (Tools\Portfolio\manifest.txt) from the committed state (HEAD) of this repository
rem into the portfolio repository. Works the same on every PC. Does not push, and does not create a GitHub repository.
rem   sync_portfolio.bat                         target = <parent of this repository>\AimRacing2026_Portfolio
rem   sync_portfolio.bat -Target E:\AimRacing\AimRacing2026_Portfolio
rem   sync_portfolio.bat -Commit                 commit the changes after writing
rem   sync_portfolio.bat -Remote https://github.com/<account>/AimRacing2026_Portfolio.git
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync_portfolio.ps1" %*
echo.
pause
