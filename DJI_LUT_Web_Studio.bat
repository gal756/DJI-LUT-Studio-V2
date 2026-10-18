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

:: Detect repository path in WSL dynamically
for /f "tokens=*" %%i in ('%WSL_RUN% wslpath "%~dp0" 2^>nul') do set "WSL_REPO_DIR=%%i"
if "%WSL_REPO_DIR%"=="" set "WSL_REPO_DIR=/home/gal756/LUT-adder"

cls
echo =====================================================================
echo                    DJI LUT STUDIO (WEB EDITION)
echo    Starting embedded local server & launching web browser...
echo =====================================================================
echo.
echo URL: http://localhost:8080
echo.

start "" "http://localhost:8080"
%WSL_RUN% -e /usr/bin/python3 %WSL_REPO_DIR%/dji_lut_studio/web_server.py 8080
pause
