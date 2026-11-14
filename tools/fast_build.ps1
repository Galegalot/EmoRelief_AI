# Fast C# Recompiler and Game Launcher for EmoRelief AI
# Compiles Assets\Scripts\*.cs directly into the Windows standalone build in ~1 second without opening Unity!

param(
    [switch]$Run,
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EmoRelief AI - Fast Code Recompiler" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Locate Unity Editor and Roslyn Compiler
$unityEditorDir = $null

if ($env:UNITY_DIR -and (Test-Path "$env:UNITY_DIR\Data\NetCoreRuntime\dotnet.exe")) {
    $unityEditorDir = $env:UNITY_DIR
} else {
    # Read preferred version from ProjectVersion.txt
    $preferredVersion = $null
    $versionFile = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
    if (Test-Path $versionFile) {
        $versionLine = Get-Content $versionFile | Where-Object { $_ -match "^m_EditorVersion:\s*(.+)$" } | Select-Object -First 1
        if ($versionLine -match "^m_EditorVersion:\s*(.+)$") {
            $preferredVersion = $Matches[1].Trim()
        }
    }

    if ($preferredVersion -and (Test-Path "C:\Program Files\Unity\Hub\Editor\$preferredVersion\Editor\Data\NetCoreRuntime\dotnet.exe")) {
        $unityEditorDir = "C:\Program Files\Unity\Hub\Editor\$preferredVersion\Editor"
    } else {
        # Search Unity Hub editors
        $candidates = Get-ChildItem -Path "C:\Program Files\Unity\Hub\Editor" -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path "$($_.FullName)\Editor\Data\NetCoreRuntime\dotnet.exe" }
        if ($candidates) {
            $unityEditorDir = "$($candidates[0].FullName)\Editor"
        }
    }
}

if (-not $unityEditorDir -or -not (Test-Path "$unityEditorDir\Data\NetCoreRuntime\dotnet.exe")) {
    Write-Host "[ERROR] Could not find a compatible Unity installation with NetCoreRuntime." -ForegroundColor Red
    Write-Host "Please set the UNITY_DIR environment variable to your Unity Editor folder." -ForegroundColor Yellow
    Write-Host "Example: set UNITY_DIR=C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor" -ForegroundColor Gray
    if (-not $NoPause) { Read-Host "Press Enter to exit..." }
    exit 1
}

$dotnet = Join-Path $unityEditorDir "Data\NetCoreRuntime\dotnet.exe"
$cscDll = Join-Path $unityEditorDir "Data\DotNetSdkRoslyn\csc.dll"

if (-not (Test-Path $cscDll)) {
    Write-Host "[ERROR] Could not find Roslyn compiler at: $cscDll" -ForegroundColor Red
    if (-not $NoPause) { Read-Host "Press Enter to exit..." }
    exit 1
}

# 2. Check standalone target directory
$targetDll = Join-Path $projectRoot "Builds\Windows\EmoRelief_Data\Managed\Assembly-CSharp.dll"
$gameExe   = Join-Path $projectRoot "Builds\Windows\EmoRelief.exe"

if (-not (Test-Path $targetDll) -or -not (Test-Path $gameExe)) {
    Write-Host "[ERROR] Target build not found at: Builds\Windows\EmoRelief.exe" -ForegroundColor Red
    Write-Host "Please ensure an initial build exists in Builds\Windows\." -ForegroundColor Yellow
    if (-not $NoPause) { Read-Host "Press Enter to exit..." }
    exit 1
}

# 3. Close existing game instance if running (to prevent file lock on Assembly-CSharp.dll)
$runningGames = Get-Process -Name "EmoRelief" -ErrorAction SilentlyContinue
if ($runningGames) {
    Write-Host "Closing running EmoRelief game instance..." -ForegroundColor Yellow
    foreach ($proc in $runningGames) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 500
}

# 4. Prepare response file
$templateRsp = Join-Path $projectRoot "tools\fast_build_template.rsp"
if (-not (Test-Path $templateRsp)) {
    $fallbackBeeRsp = Join-Path $projectRoot "Library\Bee\artifacts\1900b0aPDevDbg.dag\Assembly-CSharp.rsp"
    if (Test-Path $fallbackBeeRsp) {
        $templateRsp = $fallbackBeeRsp
    } else {
        Write-Host "[ERROR] Missing build template: $templateRsp" -ForegroundColor Red
        if (-not $NoPause) { Read-Host "Press Enter to exit..." }
        exit 1
    }
}

# Read template and adapt Unity paths and output DLL
$templateLines = [System.IO.File]::ReadAllLines($templateRsp)
$rspLines = [System.Collections.Generic.List[string]]::new()
$normalizedEditor = $unityEditorDir.Replace('\', '/')
$targetDllRel = "Builds/Windows/EmoRelief_Data/Managed/Assembly-CSharp.dll"

$rspLines.Add("-out:`"$targetDllRel`"")

foreach ($line in $templateLines) {
    # Skip any existing output / refout / source paths
    if ($line.StartsWith("-out:") -or $line.StartsWith("-refout:")) { continue }
    if ($line -like '"Assets/Scripts/*') { continue }

    # Normalize Unity Editor path in case editor path changed
    $adjustedLine = [System.Text.RegularExpressions.Regex]::Replace(
        $line,
        "C:/Program Files/Unity/Hub/Editor/[^/]+/Editor",
        $normalizedEditor,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    $rspLines.Add($adjustedLine)
}

# Add all current .cs files in Assets\Scripts
$scriptFiles = Get-ChildItem -Path (Join-Path $projectRoot "Assets\Scripts") -Recurse -Filter "*.cs"
if ($scriptFiles.Count -eq 0) {
    Write-Host "[ERROR] No C# script files found in Assets\Scripts!" -ForegroundColor Red
    if (-not $NoPause) { Read-Host "Press Enter to exit..." }
    exit 1
}

foreach ($sf in $scriptFiles) {
    # Use forward-slash relative path
    $rel = "Assets/Scripts/" + (Resolve-Path -Path $sf.FullName -Relative).Replace('.\Assets\Scripts\', '').Replace('\', '/')
    $rspLines.Add("`"$rel`"")
}

$tempRsp = Join-Path $projectRoot "fast_build_tmp.rsp"
[System.IO.File]::WriteAllLines($tempRsp, $rspLines)

# 5. Compile
Write-Host "Compiling $($scriptFiles.Count) scripts..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # Invoke dotnet csc with relative temp rsp (to avoid spaces in path issues)
    $compileOutput = & $dotnet $cscDll "@fast_build_tmp.rsp" 2>&1
    $exitCode = $LASTEXITCODE
} finally {
    Remove-Item $tempRsp -ErrorAction SilentlyContinue
}

$sw.Stop()

if ($exitCode -ne 0) {
    Write-Host "`n[COMPILE FAILED] (Exit code: $exitCode)" -ForegroundColor Red
    Write-Host "----------------------------------------" -ForegroundColor Red
    foreach ($outLine in $compileOutput) {
        Write-Host $outLine -ForegroundColor Yellow
    }
    Write-Host "----------------------------------------" -ForegroundColor Red
    if (-not $NoPause) { Read-Host "Press Enter to continue..." }
    exit $exitCode
}

$duration = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
Write-Host "[SUCCESS] Compiled into Assembly-CSharp.dll in $duration seconds!" -ForegroundColor Green

# 6. Launch game if requested
if ($Run) {
    Write-Host "Launching EmoRelief AI..." -ForegroundColor Green
    Start-Process -FilePath $gameExe -WorkingDirectory (Join-Path $projectRoot "Builds\Windows")
} elseif (-not $NoPause) {
    Write-Host "`nBuild complete. You can run the game now." -ForegroundColor Gray
    Read-Host "Press Enter to exit..."
}

exit 0
