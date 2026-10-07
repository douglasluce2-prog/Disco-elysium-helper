@echo off
rem Double-click this file to build Disco Dictionary and install it into your game.
rem It runs build.ps1 (PowerShell) without changing your system settings.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
echo.
pause
