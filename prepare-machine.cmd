@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\prepare-machine.ps1"
set "COLLABY_EXIT_CODE=%errorlevel%"
pause
exit /b %COLLABY_EXIT_CODE%
