@echo off
REM Build NeuroMod in Release configuration
echo Building NeuroMod (Release)...
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

echo Using MSBuild: %MSBUILD_PATH%
echo.

REM Build the solution
"%MSBUILD_PATH%" "Put Neuro Into a Dupe.sln" /p:Configuration=Release /t:NeurosControl /v:minimal /nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo BUILD SUCCESSFUL!
echo Output: NeuroMod\bin\Release\NeuroMod.dll
echo.
pause
