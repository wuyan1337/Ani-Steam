@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall-AniSteam.ps1"
if errorlevel 1 (
  echo Uninstall failed. Close Animeko and check the message above.
  pause
  exit /b 1
)
pause
