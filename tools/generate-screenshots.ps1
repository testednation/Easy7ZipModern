# Regenerates docs/screenshots/*.png from the live v1.4.0 app (dark theme).
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\generate-screenshots.ps1
# NOTE: keep this file pure ASCII (no emoji) - Windows PowerShell 5.1 misparses
# non-ASCII scripts saved without a BOM. UI elements are located by AutomationId
# (WPF x:Name), which is more robust than display text anyway.
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
. (Join-Path $PSScriptRoot "uia-helpers.ps1")

$buildExe = Join-Path $RepoRoot "src\bin\x64\Release\net48\Easy7ZipModern.exe"
if (-not (Test-Path $buildExe)) { throw "App not found: $buildExe (build first)" }

# The app needs the 7-Zip core (7z.exe/7z.dll), Codecs and bin beside it.
# Run the exe from the repo root (same layout build-installers.ps1 uses).
$exePath = Join-Path $RepoRoot "Easy7ZipModern.exe"
Copy-Item $buildExe $exePath -Force
$shotsDir = Join-Path $RepoRoot "docs\screenshots"
if (-not (Test-Path $shotsDir)) { New-Item -ItemType Directory -Path $shotsDir -Force | Out-Null }

# ---------- build a demo archive with a nested structure ----------
$demoRoot = Join-Path $env:TEMP ("e7z_demo_" + [Guid]::NewGuid().ToString("N").Substring(0,8))
$demoSub  = Join-Path $demoRoot "src"
$demoDocs = Join-Path $demoRoot "docs"
New-Item -ItemType Directory -Path $demoSub, $demoDocs -Force | Out-Null
Set-Content -Path (Join-Path $demoRoot "README.md")   -Value "# Demo Project`r`nSample archive for screenshots."
Set-Content -Path (Join-Path $demoRoot "setup.ini")   -Value "[App]`r`nName=Demo"
Set-Content -Path (Join-Path $demoSub   "Program.cs") -Value "class Program { static void Main() {} }"
Set-Content -Path (Join-Path $demoSub   "Utils.cs")   -Value "class Utils { }"
Set-Content -Path (Join-Path $demoDocs  "notes.txt")  -Value "Design notes for the demo project."

$demoArchive = Join-Path $env:TEMP "e7z_demo_project.7z"
if (Test-Path $demoArchive) { Remove-Item $demoArchive -Force }
$sevenZip = Join-Path $RepoRoot "7z.exe"
if (-not (Test-Path $sevenZip)) { throw "7z.exe not found at repo root" }
& $sevenZip a -t7z $demoArchive (Join-Path $demoRoot "*") | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to create demo archive" }
Write-Host "Demo archive: $demoArchive"

# destination folder pre-filled on the Smart Extractor page
$demoDest = Join-Path $env:TEMP "e7z_demo_extract"

# ---------- pin settings.json so a plain file arg opens the BROWSER ----------
# (EnableDoubleClickQuickExtract=true would route to the auto-closing QuickExtract window)
$appDataDir = Join-Path $env:APPDATA "Easy7ZipModern"
$settingsPath = Join-Path $appDataDir "settings.json"
$settingsBackup = "$settingsPath.screenshot-bak"
$hadSettings = Test-Path $settingsPath
if ($hadSettings) { Move-Item $settingsPath $settingsBackup -Force }
New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null
@"
{""IsDarkMode"":true,""ThemeName"":""Dark"",""EnableDoubleClickQuickExtract"":false,""RememberPasswords"":true,""AutoTrySavedPasswords"":true,""OpenFolderAfterExtraction"":false,""DeleteSourceAfterExtraction"":false,""OverwriteMode"":""Overwrite"",""UiScale"":1.0,""EnableContextMenu"":false,""ContextMenuCascaded"":false,""ContextMenuIcons"":true,""ContextMenuOpen"":true,""ContextMenuExtractFiles"":true,""ContextMenuExtractHere"":true,""ContextMenuExtractTo"":true,""ContextMenuTest"":true,""ContextMenuAdd"":true,""ContextMenuAdd7z"":true,""ContextMenuAddZip"":true,""ContextMenuCrcSha"":true,""ContextMenuScanFileType"":true}
"@ | ForEach-Object { $_ -replace '""', '"' } | Set-Content -Path $settingsPath -Encoding ASCII

function Wait-MainWindow {
    param([System.Diagnostics.Process]$P, [int]$TimeoutSec = 25)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $P.Refresh()
        if ($P.HasExited) { throw "App exited early with code $($P.ExitCode)" }
        if ($P.MainWindowHandle -ne [IntPtr]::Zero) { return $P.MainWindowHandle }
        Start-Sleep -Milliseconds 300
    }
    throw "Timed out waiting for main window handle"
}

$proc = $null
$scanProc = $null
try {
    # ---------- launch the main window with the demo archive open ----------
    Write-Host "Launching app with demo archive..."
    $proc = Start-Process -FilePath $exePath -ArgumentList "`"$demoArchive`"" -PassThru
    $hwnd = Wait-MainWindow -P $proc
    Write-Host "Main window handle: $hwnd"
    Focus-Window -Hwnd $hwnd
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)

    # 01 - Archive Browser with the demo archive loaded (root)
    Save-WindowShot -Hwnd $hwnd -OutFile (Join-Path $shotsDir "01-browser-root.png")

    # 02 - Smart Extractor: prefill source/dest, run a real extraction, capture progress
    $navExtract = Find-UiaById -Root $root -Id "TabNavExtractor"
    Invoke-UiaClick $navExtract
    Start-Sleep -Milliseconds 700

    $txtSource = Find-UiaById -Root $root -Id "TxtSourceArchive"
    if ($null -eq $txtSource) { throw "TxtSourceArchive not found" }
    Set-UiaText $txtSource $demoArchive

    $txtDest = Find-UiaById -Root $root -Id "TxtDestDir"
    if ($null -eq $txtDest) { throw "TxtDestDir not found" }
    Set-UiaText $txtDest $demoDest

    # keep Explorer from stealing focus/overlapping the window on success
    $chkOpen = Find-UiaById -Root $root -Id "ChkOpenFolderAfter"
    if ($null -ne $chkOpen) {
        $toggle = $chkOpen.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        if ($toggle.ToggleState -eq [System.Windows.Automation.ToggleState]::On) { $toggle.Toggle() }
    }

    $btnStart = Find-UiaById -Root $root -Id "BtnStartSmartExtract"
    Invoke-UiaClick $btnStart
    Start-Sleep -Milliseconds 900          # mid-extraction, progress bar visible
    Save-WindowShot -Hwnd $hwnd -OutFile (Join-Path $shotsDir "02-smart-extractor.png")
    Start-Sleep -Seconds 6                  # let it finish (opens explorer on success)

    # back to browser for a consistent main-window state afterwards
    $navBrowser = Find-UiaById -Root $root -Id "TabNavBrowser"
    Invoke-UiaClick $navBrowser
    Start-Sleep -Milliseconds 500

    # 03 - Components & Updates
    $navComponents = Find-UiaById -Root $root -Id "TabNavComponents"
    Invoke-UiaClick $navComponents
    Start-Sleep -Milliseconds 900
    Save-WindowShot -Hwnd $hwnd -OutFile (Join-Path $shotsDir "03-components.png")

    # 04 - Settings & Vault
    $navSettings = Find-UiaById -Root $root -Id "TabNavSettings"
    Invoke-UiaClick $navSettings
    Start-Sleep -Milliseconds 900
    Save-WindowShot -Hwnd $hwnd -OutFile (Join-Path $shotsDir "04-settings.png")

    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 800
    if (-not $proc.HasExited) { $proc.Kill() }
    $proc = $null

    # ---------- 05 - file-type scanner (/scan on a binary) ----------
    Write-Host "Launching scanner window (/scan)..."
    $scanProc = Start-Process -FilePath $exePath -ArgumentList "/scan", "`"$exePath`"" -PassThru
    $scanHwnd = Wait-MainWindow -P $scanProc
    Focus-Window -Hwnd $scanHwnd -X 60 -Y 60 -W 1200 -H 620
    Start-Sleep -Seconds 7                  # TrID analysis time
    Save-WindowShot -Hwnd $scanHwnd -OutFile (Join-Path $shotsDir "05-file-scanner.png")
    $scanProc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 500
    if (-not $scanProc.HasExited) { $scanProc.Kill() }
    $scanProc = $null

    Write-Host "Done. Screenshots in $shotsDir"
}
finally {
    if ($proc -and -not $proc.HasExited) { try { $proc.Kill() } catch { } }
    if ($scanProc -and -not $scanProc.HasExited) { try { $scanProc.Kill() } catch { } }
    if (Test-Path $demoRoot)  { Remove-Item $demoRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $demoArchive) { Remove-Item $demoArchive -Force -ErrorAction SilentlyContinue }
    if (Test-Path $demoDest)  { Remove-Item $demoDest -Recurse -Force -ErrorAction SilentlyContinue }
    # restore the user's real settings
    Remove-Item $settingsPath -Force -ErrorAction SilentlyContinue
    if ($hadSettings -and (Test-Path $settingsBackup)) { Move-Item $settingsBackup $settingsPath -Force }
}
