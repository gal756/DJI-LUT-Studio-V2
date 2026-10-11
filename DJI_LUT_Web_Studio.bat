@echo off
chcp 65001 >nul
title DJI LUT Studio - Web Application
color 0B

wsl.exe -d Ubuntu-26.04 -e echo test >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set WSL_RUN=wsl.exe -d Ubuntu-26.04
) else (
    set WSL_RUN=wsl.exe
)

cls
echo =====================================================================
echo                    DJI LUT STUDIO (WEB EDITION)
echo    Starting embedded local server & launching web browser...
echo =====================================================================
echo.
echo URL: http://localhost:8080
echo.

start "" "http://localhost:8080"
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/web_server.py 8080
pause
