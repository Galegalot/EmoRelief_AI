@echo off
setlocal
cd /d "%~dp0"
title EmoRelief AI - Quick Compile and Run

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fast_build.ps1" -Run

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Compilation failed. Please fix the errors above.
    pause
    exit /b %ERRORLEVEL%
)

endlocal
