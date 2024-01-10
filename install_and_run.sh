#!/bin/bash

# Function to install .NET 8
install_dotnet() {
    echo "Installing .NET 8..."
    # Add commands to install .NET 8 depending on your OS
    # e.g., sudo apt-get install -y dotnet-sdk-8 (for Ubuntu)
}

# Check for .NET 8
dotnet --list-sdks | grep '8.' &> /dev/null
if [ $? -ne 0 ]; then
    install_dotnet
else
    echo ".NET 8 is already installed."
fi

# Run the bot
echo "Running HartsyBot..."
dotnet run
