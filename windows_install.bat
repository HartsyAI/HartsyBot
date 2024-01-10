@echo off
SETLOCAL

:: Check for .NET 8
dotnet --list-sdks | findstr "8."
if %errorlevel% neq 0 (
    echo .NET 8 is not installed. Please install .NET 8 SDK.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b
) else (
    echo .NET 8 is already installed.
)

:: Navigate to the project directory
cd %~dp0

:: Run the bot
echo Running HartsyBot...
dotnet run

:: Prevent the command window from closing immediately
pause