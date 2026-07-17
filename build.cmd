@echo off
setlocal
set "DOTNET=%~dp0.tools\dotnet\dotnet.exe"
if not exist "%DOTNET%" (
  echo Project .NET SDK was not found.
  exit /b 1
)
"%DOTNET%" build "%~dp0DesktopWidgets.csproj" -c Release
