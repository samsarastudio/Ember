@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Start-InternetTest.ps1" -LaunchWindows
pause
