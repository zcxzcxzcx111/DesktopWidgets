@echo off
setlocal
set "EXE=%~dp0bin\Release\net8.0-windows\DesktopWidgets.exe"
if not exist "%EXE%" call "%~dp0build.cmd"
start "" "%EXE%"
