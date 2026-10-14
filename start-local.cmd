@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\local.ps1"
set "COLLABY_EXIT_CODE=%errorlevel%"
if errorlevel 1 pause
exit /b %COLLABY_EXIT_CODE%
