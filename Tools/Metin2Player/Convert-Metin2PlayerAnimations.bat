@echo off
setlocal
title Metin2 Player Animation Converter
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Convert-Metin2PlayerAnimations.ps1" -ProjectRoot "C:\Metin4\Metin3 Test"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
echo Conversion finished with exit code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%
