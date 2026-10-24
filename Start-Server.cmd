@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
pwsh -NoProfile -File "%~dp0tools\Start-Server.ps1" %*
if errorlevel 1 (
    echo.
    echo Server stopped with an error. Review the log above.
    pause
)
