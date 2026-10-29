@echo off
rem Builds bin\TimeZoneWidgets.exe using the compiler built into Windows. No installs needed.
rem   build.cmd          build only
rem   build.cmd -Sign    build and code-sign the exe (see README, "Code signing")
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
