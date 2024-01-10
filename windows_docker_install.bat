@echo off
SETLOCAL

:: Check if Docker is installed
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: Docker is not installed.
    exit /b
)

:: Build the Docker image
echo Building Docker image for HartsyBot...
docker build -t hartsybot .

:: Run the Docker container
echo Running HartsyBot in Docker...
docker run -d --name hartsybot-instance hartsybot

:: Prevent the command window from closing immediately
pause