@echo off
setlocal
cd /d "%~dp0"
title EmoRelief AI - Compile Only

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fast_build.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Compilation failed.
    pause
    exit /b %ERRORLEVEL%
)

endlocal
