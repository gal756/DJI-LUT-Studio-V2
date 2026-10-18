@echo off
chcp 65001 >nul
title DJI LUT Studio - Open Source Video Color Grading Suite
color 0B

:: Detect WSL distro
wsl.exe -d Ubuntu-26.04 -e echo test >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set WSL_RUN=wsl.exe -d Ubuntu-26.04
) else (
    set WSL_RUN=wsl.exe
)

:: If files or folders were dragged onto the batch file, go directly to DRAG_DROP mode
if not "%~1"=="" goto DRAG_DROP

:MENU
cls
echo =====================================================================
echo                    DJI LUT STUDIO (OPEN SOURCE)
echo    Universal 3D LUT Color Grading Suite for DJI Drones & Cameras
echo         Accelerated by NVIDIA RTX NVENC (10-Bit Color Pipeline)
echo =====================================================================
echo.
echo What would you like to open?
echo.
echo   [1] Native Windows Desktop GUI (Recommended)
echo   [2] Modern Web Studio (Opens in your web browser)
echo   [3] Interactive Terminal Grading Wizard
echo   [4] Start Folder Watcher Mode (Auto-grades D:\dji\incoming)
echo   [5] View All 49 DJI LUTs & Supported Devices
echo   [6] Grade all clips in D:\dji (Auto-Detect)
echo   [7] Launch Gyroflow Video Stabilization (Standalone GUI)
echo   [8] Exit
echo.
set /p MENU_CHOICE="Enter choice (1-8) [Default: 1]: "
if "%MENU_CHOICE%"=="" set MENU_CHOICE=1

if "%MENU_CHOICE%"=="1" goto LAUNCH_WPF_GUI
if "%MENU_CHOICE%"=="2" goto LAUNCH_WEB_GUI
if "%MENU_CHOICE%"=="3" goto LAUNCH_CLI
if "%MENU_CHOICE%"=="4" goto LAUNCH_WATCHER
if "%MENU_CHOICE%"=="5" goto LIST_LUTS
if "%MENU_CHOICE%"=="6" goto BATCH_D_DJI
if "%MENU_CHOICE%"=="7" goto LAUNCH_GYROFLOW
if "%MENU_CHOICE%"=="8" exit /b
goto MENU

:LAUNCH_GYROFLOW
cls
echo [INFO] Launching Gyroflow Desktop GUI...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --open-gyroflow
echo.
pause
goto MENU

:LAUNCH_WPF_GUI
cls
echo [INFO] Launching DJI LUT Studio Desktop GUI...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "\\wsl.localhost\Ubuntu-26.04\home\gal756\LUT-adder\gui\DJI_LUT_Studio_GUI.ps1"
if %ERRORLEVEL% NEQ 0 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0gui\DJI_LUT_Studio_GUI.ps1"
)
exit /b

:LAUNCH_WEB_GUI
cls
echo [INFO] Starting DJI LUT Studio Web Server on http://localhost:8080 ...
start "" "http://localhost:8080"
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/web_server.py 8080
pause
goto MENU

:LAUNCH_CLI
cls
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py -i
pause
goto MENU

:LAUNCH_WATCHER
cls
echo =====================================================================
echo                  FOLDER WATCHER ACTIVE
echo =====================================================================
echo Drop raw DJI clips into: D:\dji\incoming
echo Graded clips appear in:  D:\dji\graded
echo Look style:              Vibrant Pop (Recommended)
echo.
echo (Press Ctrl+C to stop watching)
echo =====================================================================
echo.
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py -w "D:\dji\incoming" -o "D:\dji\graded" --look vibrant -p
pause
goto MENU

:LIST_LUTS
cls
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --list-luts
echo.
pause
goto MENU

:BATCH_D_DJI
cls
echo [INFO] Scanning D:\dji for DJI video clips...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py "D:\dji" --look vibrant --sharpen -p
echo.
pause
goto MENU

:: =====================================================================
:: DRAG & DROP MODE
:: =====================================================================
:DRAG_DROP
cls
echo =====================================================================
echo                    DJI LUT STUDIO (DRAG & DROP)
echo       Accelerated by NVIDIA RTX NVENC (10-Bit Rec.709 3D LUT)
echo =====================================================================
echo.
echo [INPUT] Videos/folders detected for grading.
echo.
echo Choose Look Style:
echo   [1] Vibrant Pop     (Recommended: Punchy Sky/Grass, Micro-Contrast & Sharpness)
echo   [2] Natural Rec.709 (100%% Official DJI Baseline Color)
echo   [3] Cinematic Film  (Filmic 35mm Curve & Deep Rich Shadows)
echo   [4] Golden Hour     (Warm Golden-Hour Sunset Glow)
echo   [5] Quick 1080p     (Fast 1080p Proxy for WhatsApp / Discord / Quick share)
echo   [6] Gyroflow + Pop  (Stabilize footage with Gyroflow first, then grade)
echo.
echo Auto-starting [1] Vibrant Pop in 5 seconds... (Press 1-6 to choose now)
echo.

choice /c 123456 /d 1 /t 5 /m "Select option"
if errorlevel 6 goto LOOK_GYROFLOW
if errorlevel 5 goto LOOK_1080P
if errorlevel 4 goto LOOK_GOLDEN
if errorlevel 3 goto LOOK_CINEMATIC
if errorlevel 2 goto LOOK_NATURAL
if errorlevel 1 goto LOOK_VIBRANT

:LOOK_GYROFLOW
cls
echo [INFO] Processing with GYROFLOW STABILIZATION + VIBRANT POP...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py -g --look vibrant --sharpen -p %*
goto FINISHED

:LOOK_VIBRANT
cls
echo [INFO] Processing with VIBRANT POP look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --look vibrant --sharpen -p %*
goto FINISHED

:LOOK_NATURAL
cls
echo [INFO] Processing with NATURAL REC.709 look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --look natural -p %*
goto FINISHED

:LOOK_CINEMATIC
cls
echo [INFO] Processing with CINEMATIC FILM look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --look cinematic --sharpen -p %*
goto FINISHED

:LOOK_GOLDEN
cls
echo [INFO] Processing with GOLDEN HOUR look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --look golden -p %*
goto FINISHED

:LOOK_1080P
cls
echo [INFO] Processing with QUICK 1080P PROXY...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/LUT-adder/dji_lut_studio/cli.py --look vibrant --scale 1080p -p %*
goto FINISHED

:FINISHED
echo.
echo =====================================================================
echo Done! Graded videos are saved in the 'graded' folder next to your clips.
echo =====================================================================
echo.
pause
exit /b
