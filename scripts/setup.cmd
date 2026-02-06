@echo off
setlocal

for %%I in ("%~dp0.") do set "SCRIPT_DIR=%%~fI"
for %%I in ("%SCRIPT_DIR%\\..") do set "REPO_DIR=%%~fI"
set "DEPOT_DIR=%REPO_DIR%\depot_tools"
set "WEBRTC_ROOT=%REPO_DIR%\webrtc_build"

where pwsh >nul 2>&1
if errorlevel 1 (
  echo pwsh not found. Please install PowerShell 7 or run setup.ps1 in Windows PowerShell.
  exit /b 1
)

pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\\setup.ps1" -DepotToolsDir "%DEPOT_DIR%" -WebRtcRoot "%WEBRTC_ROOT%" %*
