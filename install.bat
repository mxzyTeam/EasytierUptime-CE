@echo off
chcp 65001 >nul

echo ========================================
echo    EasyTier Node Monitor - Complete Installation Script
echo ========================================
echo.

REM Check if Python is installed
echo [1/9] Checking Python installation...
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python not found!
    echo Please install Python 3.8+ from https://python.org
    pause
    exit /b 1
)
echo OK Python is installed

REM Check Python version
echo [2/9] Checking Python version...
for /f "tokens=2" %%i in ('python --version 2^>^&1') do set PYTHON_VERSION=%%i
echo Python version: %PYTHON_VERSION%

REM Create virtual environment
echo [3/9] Creating virtual environment...
if exist ".venv" (
    echo Virtual environment already exists, skipping...
) else (
    python -m venv .venv
    echo OK Virtual environment created
)

REM Activate virtual environment and install dependencies
echo [4/9] Installing Python dependencies...
.venv\Scripts\activate.bat && pip install --upgrade pip
if %errorlevel% neq 0 (
    echo ERROR: Failed to upgrade pip
    pause
    exit /b 1
)

.venv\Scripts\activate.bat && pip install -r requirements.txt
if %errorlevel% neq 0 (
    echo ERROR: Failed to install dependencies
    pause
    exit /b 1
)
echo OK Dependencies installed successfully

REM Check MySQL installation
echo [5/9] Checking MySQL installation...
mysql --version >nul 2>&1
if %errorlevel% neq 0 (
    echo WARNING: MySQL not found or not in PATH
    echo.
    echo Would you like to install MySQL? (Y/N)
    set /p MYSQL_CHOICE=
    if /i "%MYSQL_CHOICE%"=="Y" (
        echo Installing MySQL...
        echo Please download and install MySQL from: https://dev.mysql.com/downloads/mysql/
        echo After installation, please add MySQL to your PATH and restart this script.
        pause
        exit /b 0
    ) else (
        echo Please ensure MySQL is installed and running before continuing.
    )
) else (
    echo OK MySQL is installed
)

REM Create environment configuration file if not exists
echo [6/9] Checking environment configuration...
if not exist ".env" (
    echo Creating default .env configuration file...
    (
echo # Database Configuration
echo MYSQL_HOST=localhost
echo MYSQL_USER=root
echo MYSQL_PASSWORD=your_password_here
echo MYSQL_DATABASE=easytier_monitor
echo MYSQL_PORT=3306
echo.
echo # Flask Configuration
echo FLASK_ENV=development
echo FLASK_DEBUG=True
echo SECRET_KEY=your_secret_key_here
    ) > .env
    echo Please edit .env file with your MySQL credentials
    echo.
)

REM Setup database
echo [7/9] Setting up database...
.venv\Scripts\activate.bat && cd backend && python create_tables.py
if %errorlevel% neq 0 (
    echo ERROR: Failed to create database tables
    echo Please check MySQL configuration in .env file
    echo Make sure MySQL service is running and credentials are correct
    pause
    exit /b 1
)
echo OK Database setup completed

REM Test the installation
echo [8/9] Testing installation...
.venv\Scripts\activate.bat && cd backend && python -c "import flask; import pymysql; print('OK All modules imported successfully')"
if %errorlevel% neq 0 (
    echo ERROR: Module import test failed
    pause
    exit /b 1
)

REM Create startup script
echo [9/9] Creating startup shortcuts...
if not exist "start.bat" (
    echo Creating start.bat for easy startup...
    echo @echo off > start.bat
    echo chcp 65001 ^>nul >> start.bat
    echo echo Starting EasyTier Monitor... >> start.bat
    echo .venv\Scripts\activate.bat ^&^& cd backend ^&^& python app.py >> start.bat
    echo OK Start script created
)

echo.
echo ========================================
echo   Installation Completed Successfully!
echo ========================================
echo.
echo IMPORTANT: Before running the system:
echo 1. Edit .env file with your MySQL credentials
echo 2. Ensure MySQL service is running
echo.
echo Next steps:
echo 1. Run start_all.bat to start the full system
echo 2. Or run start.bat to start only the Flask server
echo 3. Open http://127.0.0.1:5000 in your browser
echo 4. Check scripts\project_info.py for system information
echo.
echo Installation summary:
echo - Python environment: Ready
echo - Virtual environment: Created
echo - Dependencies: Installed
echo - Database: Configured
echo - Startup scripts: Created
echo.
echo Press any key to close this window...
pause >nul