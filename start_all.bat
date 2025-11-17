@echo off
chcp 65001 >nul

echo ========================================
echo    EasyTier Node Monitor - Start Script
echo ========================================
echo.

REM Check Python environment
echo [1/4] Checking Python environment...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python not found, please install Python first
    pause
    exit /b 1
)
echo OK Python environment

REM Check virtual environment
echo [2/4] Checking virtual environment...
if not exist ".venv\Scripts\activate.bat" (
    echo WARNING: Virtual environment not found, using system Python
    set VENV_ACTIVATE=
) else (
    echo OK Virtual environment exists
    set VENV_ACTIVATE=.venv\Scripts\activate.bat
)

REM Start Flask server
echo [3/4] Starting Flask Web server...
start "Flask Server" cmd /k "%VENV_ACTIVATE% && cd backend && python app.py"
echo OK Flask server started (port 5000)

REM Start data sync service
echo [4/4] Starting data sync service...
start "Data Sync" cmd /k "%VENV_ACTIVATE% && cd backend && python dev.py"
echo OK Data sync service started

echo.
echo ========================================
echo   Startup Complete!
echo ========================================
echo.
echo Access URL: http://127.0.0.1:5000
echo Data Sync: Auto-execute every 30 minutes
echo Log File: api_monitor_YYYYMMDD.log
echo.
echo Press any key to close this window...
pause >nul