@echo off
SETLOCAL

:: Change directory to the script's location
cd %~dp0

:: Navigate to the root directory of the project where Dockerfile is located
cd ..

REM Set the path to the .env file relative to this new current directory
SET ENV_FILE=.env
echo Looking for .env file at: %cd%\%ENV_FILE%

REM Check if .env file exists
IF NOT EXIST "%ENV_FILE%" (
    echo Error: .env file not found at %cd%\%ENV_FILE%
    echo Please ensure that the .env file exists and try again.
    pause
    exit /b 1
)

echo Found .env file

:: Build the Docker image
echo Building Docker image for HartsyBot...
docker build -f Docker/Dockerfile -t hartsybot .

:: Run the Docker container
echo Running HartsyBot in Docker...
docker run -d --name hartsybot-instance --env-file .env -p 7801:7801 hartsybot

:: Prevent the command window from closing immediately
pause
