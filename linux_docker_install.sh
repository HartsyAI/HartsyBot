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
# Check for .NET 8
if ! command_exists dotnet || [ -z "$(dotnet --list-sdks | grep '8.')" ]; then
    install_dotnet
else
    echo ".NET 8 SDK is already installed."
fi

echo "Checking for Docker..."
# Check if Docker is installed
if ! command_exists docker; then
    echo "Error: Docker is not installed. Please install Docker and rerun this script."
    exit 1
else
    echo "Docker is already installed."
fi

# Navigate to the project directory
cd .

# Build the Docker image
echo "Building Docker image for HartsyBot..."
docker build -t hartsybot .

# Run the Docker container
echo "Running HartsyBot in Docker..."
docker run -d --name hartsybot-instance hartsybot

# Keep the window open
echo "Press Enter to close this window..."
read