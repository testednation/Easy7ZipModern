@echo off
setlocal enabledelayedexpansion

set VERSION=1.4.0
set "ROOT_DIR=%~dp0"
if "%ROOT_DIR:~-1%"=="\" set "ROOT_DIR=%ROOT_DIR:~0,-1%"

:: Prepend all tool paths to PATH
set "PATH=C:\Program Files\dotnet;C:\temp\dotnet-sdk;C:\Program Files\Inno Setup 7;C:\temp\installer\tools\innoportable\{app};%PATH%"

echo ======================================================================
echo  Easy 7-Zip Modern v%VERSION% - Automated Build ^& Installer Generator
echo ======================================================================
echo.

:: 1. Locate dotnet.exe
set "DOTNET_EXE="
if exist "C:\Program Files\dotnet\dotnet.exe" (
    set "DOTNET_EXE=C:\Program Files\dotnet\dotnet.exe"
) else if exist "C:\temp\dotnet-sdk\dotnet.exe" (
    set "DOTNET_EXE=C:\temp\dotnet-sdk\dotnet.exe"
) else (
    where dotnet.exe >nul 2>nul
    if %errorlevel% equ 0 set "DOTNET_EXE=dotnet"
)

if "%DOTNET_EXE%"=="" (
    echo [ERROR] .NET SDK (dotnet.exe) was not found!
    echo Checked:
    echo   - C:\Program Files\dotnet\dotnet.exe
    echo   - C:\temp\dotnet-sdk\dotnet.exe
    echo   - System PATH
    echo Please install .NET SDK or place it in C:\temp\dotnet-sdk\
    pause
    exit /b 1
)
echo [OK] Found .NET SDK: "%DOTNET_EXE%"

:: 2. Locate ISCC.exe
set "ISCC_EXE="
if exist "C:\Program Files\Inno Setup 7\ISCC.exe" (
    set "ISCC_EXE=C:\Program Files\Inno Setup 7\ISCC.exe"
) else if exist "C:\temp\installer\tools\innoportable\{app}\ISCC.exe" (
    set "ISCC_EXE=C:\temp\installer\tools\innoportable\{app}\ISCC.exe"
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC_EXE=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else (
    where ISCC.exe >nul 2>nul
    if %errorlevel% equ 0 set "ISCC_EXE=ISCC.exe"
)

if "%ISCC_EXE%"=="" (
    echo [ERROR] Inno Setup Compiler (ISCC.exe) was not found!
    echo Checked:
    echo   - C:\Program Files\Inno Setup 7\ISCC.exe
    echo   - C:\temp\installer\tools\innoportable\{app}\ISCC.exe
    echo   - System PATH
    pause
    exit /b 1
)
echo [OK] Found Inno Setup Compiler: "%ISCC_EXE%"

echo.
echo [1/4] Compiling Easy 7-Zip Modern x64 (Release)...
"%DOTNET_EXE%" build "%ROOT_DIR%\src\Easy7ZipModern.csproj" -c Release -p:Platform=x64
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] dotnet build x64 failed with exit code %errorlevel%!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/4] Compiling Easy 7-Zip Modern x86 (Release)...
"%DOTNET_EXE%" build "%ROOT_DIR%\src\Easy7ZipModern.csproj" -c Release -p:Platform=x86
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] dotnet build x86 failed with exit code %errorlevel%!
    pause
    exit /b %errorlevel%
)

echo.
echo [3/4] Staging Payloads...
if not exist "C:\temp\installer\payload-x64" mkdir "C:\temp\installer\payload-x64"
if not exist "C:\temp\installer\payload-x86" mkdir "C:\temp\installer\payload-x86"
if not exist "C:\temp\installer\output" mkdir "C:\temp\installer\output"

copy /y "%ROOT_DIR%\src\bin\x64\Release\net48\Easy7ZipModern.exe" "C:\temp\installer\payload-x64\Easy7ZipModern.exe" >nul
copy /y "%ROOT_DIR%\src\bin\x64\Release\net48\Easy7ZipModern.pdb" "C:\temp\installer\payload-x64\Easy7ZipModern.pdb" >nul
copy /y "%ROOT_DIR%\src\bin\x64\Release\net48\Easy7ZipModern.exe" "%ROOT_DIR%\Easy7ZipModern.exe" >nul

copy /y "%ROOT_DIR%\src\bin\x86\Release\net48\Easy7ZipModern.exe" "C:\temp\installer\payload-x86\Easy7ZipModern.exe" >nul
copy /y "%ROOT_DIR%\src\bin\x86\Release\net48\Easy7ZipModern.pdb" "C:\temp\installer\payload-x86\Easy7ZipModern.pdb" >nul
echo Payloads staged successfully.

echo.
echo [4/4] Building Inno Setup Installers...
echo --- Compiling x64 installer ---
"%ISCC_EXE%" "%ROOT_DIR%\installer\Easy7ZipModern-installer.iss" /DArch=x64
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Inno Setup x64 installer compilation failed!
    pause
    exit /b %errorlevel%
)

echo.
echo --- Compiling x86 installer ---
"%ISCC_EXE%" "%ROOT_DIR%\installer\Easy7ZipModern-installer.iss" /DArch=x86
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Inno Setup x86 installer compilation failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Syncing output installers to repository...
if not exist "%ROOT_DIR%\installer" mkdir "%ROOT_DIR%\installer"
copy /y "C:\temp\installer\output\Easy7ZipModern-%VERSION%-setup-x64.exe" "%ROOT_DIR%\installer\Easy7ZipModern-%VERSION%-setup-x64.exe" >nul
copy /y "C:\temp\installer\output\Easy7ZipModern-%VERSION%-setup-x86.exe" "%ROOT_DIR%\installer\Easy7ZipModern-%VERSION%-setup-x86.exe" >nul

echo.
echo ======================================================================
echo  [SUCCESS] Easy 7-Zip Modern v%VERSION% Installers Generated!
echo ======================================================================
echo.
echo  x64 Installer:
echo    - %ROOT_DIR%\installer\Easy7ZipModern-%VERSION%-setup-x64.exe
echo    - C:\temp\installer\output\Easy7ZipModern-%VERSION%-setup-x64.exe
echo.
echo  x86 Installer:
echo    - %ROOT_DIR%\installer\Easy7ZipModern-%VERSION%-setup-x86.exe
echo    - C:\temp\installer\output\Easy7ZipModern-%VERSION%-setup-x86.exe
echo ======================================================================
echo.
pause
