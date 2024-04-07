#!/bin/bash

# Function to check if a command exists
command_exists() {
    type "$1" &> /dev/null
}

# Function to install .NET 8 SDK
install_dotnet() {
    echo "Installing .NET 8 SDK..."
    wget https://dot.net/v1/dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --version 8.0.100
    rm dotnet-install.sh
}

echo "Checking for .NET SDK 8..."
if ! command_exists dotnet || [ -z "$(dotnet --list-sdks | grep '8.')" ]; then
    install_dotnet
else
    echo ".NET 8 SDK is already installed."
fi

echo "Checking for Docker..."
if ! command_exists docker; then
    echo "Error: Docker is not installed. Please install Docker and rerun this script."
    exit 1
else
    echo "Docker is already installed."
fi

# Ensure the script is in the Hartsy directory where the Hartsy.csproj file is located
cd "$(dirname "$0")/.."

echo "Current directory:"
pwd

echo "Building Docker image for HartsyBot..."
# Specify the Dockerfile path relative to the context directory
docker build -f Docker/Dockerfile -t hartsybot .

echo "Running HartsyBot in Docker..."
docker run -d --name hartsybot-instance --env-file ./.env hartsybot

echo "Press Enter to close this window..."
read
