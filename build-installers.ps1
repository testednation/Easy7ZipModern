# Easy 7-Zip Modern: Build and Generate Installers
$ErrorActionPreference = "Stop"

$version = "1.4.0"
$rootDir = $PSScriptRoot
if (-not $rootDir) { $rootDir = "C:\Users\Administrator\Documents\Easy7zipm" }

$env:PATH = "C:\Program Files\dotnet;C:\temp\dotnet-sdk;C:\Program Files\Inno Setup 7;C:\temp\installer\tools\innoportable\{app};" + $env:PATH

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " Easy 7-Zip Modern v$version - Automated Build & Installer Generator" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

# 1. Locate dotnet
$dotnetCandidates = @(
    "C:\Program Files\dotnet\dotnet.exe",
    "C:\temp\dotnet-sdk\dotnet.exe"
)
$dotnetExe = $null
foreach ($c in $dotnetCandidates) {
    if (Test-Path $c) { $dotnetExe = $c; break }
}
if (-not $dotnetExe) {
    $cmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($cmd) { $dotnetExe = $cmd.Source }
}
if (-not $dotnetExe) {
    throw "[ERROR] .NET SDK (dotnet.exe) was not found!"
}
Write-Host "[OK] Using .NET SDK: $dotnetExe" -ForegroundColor Gray

# 2. Locate ISCC
$isccCandidates = @(
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\temp\installer\tools\innoportable\{app}\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)
$isccExe = $null
foreach ($c in $isccCandidates) {
    if (Test-Path $c) { $isccExe = $c; break }
}
if (-not $isccExe) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $isccExe = $cmd.Source }
}
if (-not $isccExe) {
    throw "[ERROR] Inno Setup compiler (ISCC.exe) was not found!"
}
Write-Host "[OK] Using Inno Setup: $isccExe" -ForegroundColor Gray

# 3. Build Binaries
Write-Host "`n[1/4] Building Release x64 and x86 binaries..." -ForegroundColor Yellow
$proj = "$rootDir\src\Easy7ZipModern.csproj"

Write-Host "Building x64 (Release)..." -ForegroundColor Gray
& $dotnetExe build $proj -c Release -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "dotnet build x64 failed with exit code $LASTEXITCODE" }

Write-Host "Building x86 (Release)..." -ForegroundColor Gray
& $dotnetExe build $proj -c Release -p:Platform=x86
if ($LASTEXITCODE -ne 0) { throw "dotnet build x86 failed with exit code $LASTEXITCODE" }

# 4. Stage Payloads
Write-Host "`n[2/4] Staging Payloads..." -ForegroundColor Yellow
$p64 = "C:\temp\installer\payload-x64"
$p86 = "C:\temp\installer\payload-x86"
$outDir = "C:\temp\installer\output"

@($p64, $p86, $outDir) | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
}

Copy-Item "$rootDir\src\bin\x64\Release\net48\Easy7ZipModern.exe" "$p64\Easy7ZipModern.exe" -Force
Copy-Item "$rootDir\src\bin\x64\Release\net48\Easy7ZipModern.pdb" "$p64\Easy7ZipModern.pdb" -Force
Copy-Item "$rootDir\src\bin\x64\Release\net48\Easy7ZipModern.exe" "$rootDir\Easy7ZipModern.exe" -Force

Copy-Item "$rootDir\src\bin\x86\Release\net48\Easy7ZipModern.exe" "$p86\Easy7ZipModern.exe" -Force
Copy-Item "$rootDir\src\bin\x86\Release\net48\Easy7ZipModern.pdb" "$p86\Easy7ZipModern.pdb" -Force

# 5. Compile Installers
Write-Host "`n[3/4] Compiling Installers with Inno Setup..." -ForegroundColor Yellow
$issPath = "$rootDir\installer\Easy7ZipModern-installer.iss"

Write-Host "Compiling x64 installer..." -ForegroundColor Gray
& $isccExe $issPath /DArch=x64
if ($LASTEXITCODE -ne 0) { throw "ISCC x64 compilation failed with exit code $LASTEXITCODE" }

Write-Host "Compiling x86 installer..." -ForegroundColor Gray
& $isccExe $issPath /DArch=x86
if ($LASTEXITCODE -ne 0) { throw "ISCC x86 compilation failed with exit code $LASTEXITCODE" }

# 6. Copy output
Write-Host "`n[4/4] Syncing installers to repository..." -ForegroundColor Yellow
$repoInstallerDir = "$rootDir\installer"
if (-not (Test-Path $repoInstallerDir)) {
    New-Item -ItemType Directory -Path $repoInstallerDir -Force | Out-Null
}

Copy-Item "$outDir\Easy7ZipModern-$version-setup-x64.exe" "$repoInstallerDir\Easy7ZipModern-$version-setup-x64.exe" -Force
Copy-Item "$outDir\Easy7ZipModern-$version-setup-x86.exe" "$repoInstallerDir\Easy7ZipModern-$version-setup-x86.exe" -Force

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host " [SUCCESS] Easy 7-Zip Modern v$version Installers Generated!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  x64 Installer: $repoInstallerDir\Easy7ZipModern-$version-setup-x64.exe" -ForegroundColor White
Write-Host "  x86 Installer: $repoInstallerDir\Easy7ZipModern-$version-setup-x86.exe" -ForegroundColor White
Write-Host "======================================================================" -ForegroundColor Green
