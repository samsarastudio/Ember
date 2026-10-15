@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Two-Windows-Clients.ps1"
if errorlevel 1 pause
