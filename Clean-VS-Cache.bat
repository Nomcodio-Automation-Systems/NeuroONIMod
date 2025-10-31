@echo off
echo Cleaning Visual Studio cache and build artifacts...
echo.

REM Remove VS cache directory
if exist .vs (
    echo Removing .vs directory...
    rmdir /s /q .vs
)

REM Clean NeuroMod project
if exist NeuroMod\bin (
    echo Removing NeuroMod\bin...
    rmdir /s /q NeuroMod\bin
)
if exist NeuroMod\obj (
    echo Removing NeuroMod\obj...
    rmdir /s /q NeuroMod\obj
)

REM Clean Test project  
if exist NeuroMod.Tests\bin (
    echo Removing NeuroMod.Tests\bin...
    rmdir /s /q NeuroMod.Tests\bin
)
if exist NeuroMod.Tests\obj (
    echo Removing NeuroMod.Tests\obj...
    rmdir /s /q NeuroMod.Tests\obj
)

echo.
echo Cache cleanup complete!
echo Now open Visual Studio and try rebuilding again.
echo.
pause