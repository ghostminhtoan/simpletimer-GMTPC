@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo Building TimeBomb Windows (WPF .NET Framework 4.7.2)
echo =======================================================

:: 1. Terminate running instance if any
taskkill /F /IM TimeBomb.exe >nul 2>&1
powershell -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -Command taskkill /F /IM TimeBomb.exe' -Wait" >nul 2>&1

:: 2. Find MSBuild
set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

if not exist "!MSBUILD_PATH!" (
    for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        set "MSBUILD_PATH=%%i"
    )
)

if not exist "!MSBUILD_PATH!" (
    echo [ERROR] MSBuild.exe not found!
    exit /b 1
)

echo [INFO] Using MSBuild: !MSBUILD_PATH!

:: 3. Run MSBuild Release Rebuild on project
"!MSBUILD_PATH!" "TimeBomb.csproj" /t:Rebuild /p:Configuration=Release /p:Platform="AnyCPU" /v:m /nologo

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed with exit code %ERRORLEVEL%!
    exit /b %ERRORLEVEL%
)

if not exist "bin\Release\TimeBomb.exe" (
    echo [ERROR] bin\Release\TimeBomb.exe not found!
    exit /b 1
)

echo =======================================================
echo [SUCCESS] Build completed with 0 Errors!
echo Executable: %~dp0bin\Release\TimeBomb.exe
echo =======================================================
exit /b 0
