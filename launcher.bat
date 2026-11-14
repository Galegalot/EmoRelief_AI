@echo off
setlocal
cd /d "%~dp0"
title EmoRelief AI - Launcher

:MENU
cls
echo =======================================================
echo                 EmoRelief AI - Dev Launcher
echo =======================================================
echo.
echo   [1] Fast Compile ^& Launch Game  (Recompile code in 1s ^& run)
echo   [2] Fast Compile Only          (Verify C# code without running)
echo   [3] Launch Game Directly       (Run current build without recompile)
echo   [4] Run Automated Smoke Test   (Headless Play Mode test)
echo   [5] Exit
echo.
echo =======================================================
set /p CHOICE="Select an option [1-5]: "

if "%CHOICE%"=="1" goto COMPILE_AND_RUN
if "%CHOICE%"=="2" goto COMPILE_ONLY
if "%CHOICE%"=="3" goto RUN_DIRECT
if "%CHOICE%"=="4" goto SMOKE_TEST
if "%CHOICE%"=="5" goto EXIT
echo Invalid choice. Please enter 1-5.
timeout /t 2 >nul
goto MENU

:COMPILE_AND_RUN
cls
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fast_build.ps1" -Run
echo.
pause
goto MENU

:COMPILE_ONLY
cls
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\fast_build.ps1"
echo.
pause
goto MENU

:RUN_DIRECT
cls
if not exist "Builds\Windows\EmoRelief.exe" (
    echo [ERROR] Builds\Windows\EmoRelief.exe not found!
    pause
    goto MENU
)
echo Launching EmoRelief AI...
start "" /d "Builds\Windows" "Builds\Windows\EmoRelief.exe"
timeout /t 2 >nul
goto MENU

:SMOKE_TEST
cls
echo Running Play Mode Smoke Test...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& {
    $version = (Get-Content ProjectSettings\ProjectVersion.txt | Select-String 'm_EditorVersion:\s*(.+)').Matches.Groups[1].Value.Trim();
    $unity = 'C:\Program Files\Unity\Hub\Editor\' + $version + '\Editor\Unity.exe';
    if (-not (Test-Path $unity)) {
        $c = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe' -ErrorAction SilentlyContinue | Select-Object -First 1;
        if ($c) { $unity = $c.FullName }
    }
    Write-Host \"Using Unity: $unity\";
    & $unity -force-d3d11 -burst-disable-compilation -projectPath \"$PWD\" -executeMethod EmoRelief.Editor.PrototypeBuilder.PlayPreview -emoreliefSmoke -logFile \"$PWD\Logs\playmode-smoke.log\"
}"
echo Smoke test finished. Checking log for results:
if exist "Logs\playmode-smoke.log" (
    findstr /i "EMORELIEF" "Logs\playmode-smoke.log"
)
pause
goto MENU

:EXIT
exit /b 0
