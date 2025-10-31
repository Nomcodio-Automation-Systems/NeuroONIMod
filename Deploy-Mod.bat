@echo off
REM Deploy NeuroMod DLL to ONI mods folder
echo Deploying NeuroMod to ONI...
echo.

REM Set the ONI mods folder path
set ONI_MODS=%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\local\NeuroMod

REM Determine which configuration to deploy (prefer Release if exists)
set SOURCE_DLL=
if exist "NeuroMod\bin\Release\NeuroMod.dll" (
    set SOURCE_DLL=NeuroMod\bin\Release\NeuroMod.dll
    set CONFIG=Release
) else if exist "NeuroMod\bin\Debug\NeuroMod.dll" (
    set SOURCE_DLL=NeuroMod\bin\Debug\NeuroMod.dll
    set CONFIG=Debug
) else (
    echo ERROR: No built DLL found!
    echo Please build the mod first using Build-Release.bat or Build-Debug.bat
    pause
    exit /b 1
)

echo Found DLL: %SOURCE_DLL% (%CONFIG%)
echo.

REM Create mods directory if it doesn't exist
if not exist "%ONI_MODS%" (
    echo Creating mod directory: %ONI_MODS%
    mkdir "%ONI_MODS%"
)

REM Copy the DLL
echo Copying to: %ONI_MODS%
copy /Y "%SOURCE_DLL%" "%ONI_MODS%\NeuroMod.dll"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo DEPLOY FAILED!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo DEPLOY SUCCESSFUL!
echo NeuroMod.dll copied to ONI mods folder
echo.
