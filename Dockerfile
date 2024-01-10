# Use the official image as a parent image
FROM mcr.microsoft.com/dotnet/runtime:latest

# Set the working directory
WORKDIR /app

# Copy the bot files into the container at /app
COPY . ./

# Set the entry point of the application
ENTRYPOINT ["dotnet", "YourBot.dll"]
