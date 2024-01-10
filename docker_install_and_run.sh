#!/bin/bash

# Check if Docker is installed
if ! [ -x "$(command -v docker)" ]; then
    echo "Error: Docker is not installed." >&2
    exit 1
fi

# Build the Docker image
echo "Building Docker image for HartsyBot..."
docker build -t hartsybot .

# Run the Docker container
echo "Running HartsyBot in Docker..."
docker run -d --name hartsybot hartsybot
