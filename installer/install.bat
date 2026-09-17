@echo off
REM Wrapper: install Screen Audio Router to a chosen directory.
setlocal
set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1" %*
endlocal
