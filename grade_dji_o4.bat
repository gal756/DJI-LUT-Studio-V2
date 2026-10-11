@echo off
chcp 65001 >nul
title DJI O4 Pro D-Log M Color Grading Pipeline
color 0B

:: Ensure WSL target distro is Ubuntu-26.04 or fallback
wsl.exe -d Ubuntu-26.04 -e echo test >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set WSL_RUN=wsl.exe -d Ubuntu-26.04
) else (
    set WSL_RUN=wsl.exe
)

if "%~1"=="" goto MENU

:: =====================================================================
:: DRAG & DROP MODE
:: =====================================================================
cls
echo =====================================================================
echo           DJI O4 PRO / AIR UNIT D-LOG M COLOR GRADING PIPELINE
echo       Accelerated by NVIDIA RTX NVENC (10-Bit Rec.709 3D LUT)
echo =====================================================================
echo.
echo [INPUT] Files / folders detected for processing.
echo.
echo Choose Look Style:
echo   [1] Vibrant Pop     (Recommended: punchy sky/grass, micro-contrast & sharp foliage)
echo   [2] Natural Rec.709 (100%% official DJI baseline color)
echo   [3] Cinematic Film  (Deeper contrast, filmic curve & rich shadows)
echo   [4] Golden Hour     (Warm golden-hour sunlight glow)
echo   [5] Quick 1080p     (Fast 1080p proxy for WhatsApp / Discord / Quick share)
echo.
echo Auto-starting [1] Vibrant Pop in 5 seconds... (Press 1-5 to choose now)
echo.

choice /c 12345 /d 1 /t 5 /m "Select option"
if errorlevel 5 goto LOOK_1080P
if errorlevel 4 goto LOOK_GOLDEN
if errorlevel 3 goto LOOK_CINEMATIC
if errorlevel 2 goto LOOK_NATURAL
if errorlevel 1 goto LOOK_VIBRANT

:LOOK_VIBRANT
cls
echo [INFO] Processing with VIBRANT POP look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look vibrant --preview %*
goto FINISHED

:LOOK_NATURAL
cls
echo [INFO] Processing with NATURAL REC.709 look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look natural --preview %*
goto FINISHED

:LOOK_CINEMATIC
cls
echo [INFO] Processing with CINEMATIC FILM look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look cinematic --sharpen --preview %*
goto FINISHED

:LOOK_GOLDEN
cls
echo [INFO] Processing with GOLDEN HOUR look...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look golden --preview %*
goto FINISHED

:LOOK_1080P
cls
echo [INFO] Processing with QUICK 1080P PROXY (Vibrant)...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look vibrant --scale 1080p --preview %*
goto FINISHED

:FINISHED
echo.
echo =====================================================================
echo Done! Graded videos are saved in the 'graded' folder next to your clips.
echo =====================================================================
echo.
pause
exit /b

:: =====================================================================
:: INTERACTIVE MENU
:: =====================================================================
:MENU
cls
echo =====================================================================
echo           DJI O4 PRO / AIR UNIT D-LOG M COLOR GRADING PIPELINE
echo       Accelerated by NVIDIA RTX NVENC (10-Bit Rec.709 3D LUT)
echo =====================================================================
echo.
echo What would you like to do?
echo.
echo   [1] Start Folder Watcher (Auto-grades any video dropped into D:\dji\incoming)
echo   [2] Grade all videos in D:\dji (Vibrant Pop)
echo   [3] Grade all videos in D:\dji (Natural Rec.709)
echo   [4] Grade all videos in D:\dji (Cinematic Film)
echo   [5] Enter / Paste a custom video path or folder
echo   [6] Exit
echo.
set /p MENU_CHOICE="Enter choice (1-6): "

if "%MENU_CHOICE%"=="1" goto WATCHER
if "%MENU_CHOICE%"=="2" goto BATCH_VIBRANT
if "%MENU_CHOICE%"=="3" goto BATCH_NATURAL
if "%MENU_CHOICE%"=="4" goto BATCH_CINEMATIC
if "%MENU_CHOICE%"=="5" goto CUSTOM
if "%MENU_CHOICE%"=="6" exit /b
goto MENU

:WATCHER
cls
echo =====================================================================
echo                    WATCHER MODE ACTIVATED
echo =====================================================================
echo Drop raw videos into: D:\dji\incoming
echo Graded clips appear in: D:\dji\graded
echo Look Preset: Vibrant Pop (Recommended)
echo.
echo (Press Ctrl+C to stop watching)
echo =====================================================================
echo.
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --watch "D:\dji\incoming" -o "D:\dji\graded" --look vibrant --preview
pause
exit /b

:BATCH_VIBRANT
cls
echo [INFO] Scanning and grading D:\dji with Vibrant Pop...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py "D:\dji" --look vibrant --preview
echo.
pause
exit /b

:BATCH_NATURAL
cls
echo [INFO] Scanning and grading D:\dji with Natural Rec.709...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py "D:\dji" --look natural --preview
echo.
pause
exit /b

:BATCH_CINEMATIC
cls
echo [INFO] Scanning and grading D:\dji with Cinematic Film...
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py "D:\dji" --look cinematic --sharpen --preview
echo.
pause
exit /b

:CUSTOM
echo.
set /p TARGET_PATH="Drag and drop or paste path to video or folder: "
if "%TARGET_PATH%"=="" goto MENU
cls
%WSL_RUN% -e /usr/bin/python3 /home/gal756/o4-grade-script/pipeline.py --look vibrant --preview %TARGET_PATH%
echo.
pause
exit /b
