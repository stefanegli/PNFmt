@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\build\Test-Coverage.ps1" %*
exit /b %errorlevel%
