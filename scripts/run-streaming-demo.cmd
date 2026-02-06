@echo off
setlocal

for %%I in ("%~dp0.") do set "SCRIPT_DIR=%%~fI"

where pwsh >nul 2>&1
if errorlevel 1 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\run-streaming-demo.ps1" %*
  exit /b %errorlevel%
)

pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\run-streaming-demo.ps1" %*
