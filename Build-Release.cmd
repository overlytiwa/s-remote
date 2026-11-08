@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK not found. Install .NET 8 SDK or newer from https://dotnet.microsoft.com/download
  pause
  exit /b 1
)
echo Building S-Remote release...
dotnet publish SamsungMiniRemote.csproj -c Release -r win-x64 --self-contained false -o publish
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo.
echo Build succeeded. Output is in the publish\ directory.
echo Run: publish\SamsungMiniRemote.exe
pause
