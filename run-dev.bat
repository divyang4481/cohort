@echo off
setlocal

set REPO_ROOT=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%REPO_ROOT%run-dev.ps1"

endlocal
