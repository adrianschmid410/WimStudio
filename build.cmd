@echo off
REM Build-Skript für WIM Studio
REM Erstellt eine Release-Version und veröffentlicht sie als Single-File-EXE

setlocal
set CONFIG=Release
set RUNTIME=win-x64
set OUTPUT=publish\%RUNTIME%

echo === WIM Studio Build ===
echo Konfiguration: %CONFIG%
echo Runtime:       %RUNTIME%
echo Ziel:          %OUTPUT%
echo.

echo [1/3] NuGet-Pakete wiederherstellen...
dotnet restore WimStudio.sln
if errorlevel 1 goto error

echo.
echo [2/3] Solution bauen...
dotnet build WimStudio.sln -c %CONFIG% --no-restore
if errorlevel 1 goto error

echo.
echo [3/3] Single-File veröffentlichen...
dotnet publish WimStudio\WimStudio.csproj ^
    -c %CONFIG% ^
    -r %RUNTIME% ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o %OUTPUT%
if errorlevel 1 goto error

echo.
echo === Build erfolgreich ===
echo Ausgabe: %OUTPUT%\WimStudio.exe
echo.
exit /b 0

:error
echo.
echo === BUILD FEHLGESCHLAGEN ===
exit /b 1
