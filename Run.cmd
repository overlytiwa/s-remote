@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK not found. Install .NET 8 SDK or newer from https://dotnet.microsoft.com/download
  pause
  exit /b 1
)
dotnet run --project SamsungMiniRemote.csproj
if errorlevel 1 pause
