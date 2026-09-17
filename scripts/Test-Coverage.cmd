@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-Coverage.ps1" %*
exit /b %errorlevel%
