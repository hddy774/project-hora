@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-local.ps1" -CreateDesktopShortcut -Launch
if errorlevel 1 pause
