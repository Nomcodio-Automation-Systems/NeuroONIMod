@echo off
REM Build and deploy NeuroMod in Release configuration
echo ========================================
echo Build and Deploy NeuroMod (Release)
echo ========================================
echo.

REM Try to find MSBuild
set MSBUILD_PATH=
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
) else (
    echo ERROR: Could not find MSBuild.exe
    echo Please ensure Visual Studio 2022 is installed
    pause
    exit /b 1
)

echo Step 1: Building...
echo.
"%MSBUILD_PATH%" "Put Neuro Into a Dupe.sln" /p:Configuration=Release /t:NeurosControl /v:minimal /nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo Step 2: Deploying...
echo ========================================
echo.

call Deploy-Mod.bat

echo.
echo ========================================
echo COMPLETE!
echo ========================================
pause
