# HartsyBot README

## Setup and Run

### Prerequisites
- .NET Core SDK (for manual setup)
- Docker (optional, for Docker setup)

### Setting up `.env`
1. Create a `.env` file in the root directory of the project.
2. Add your bot token and any other necessary environment variables. Example format:
BOT_TOKEN=your_bot_token_here


### Recommended One-Click Install
For ease of setup, use the provided shell scripts.

#### Regular Setup
1. Make the script executable:
chmod +x install_and_run.sh

2. Run the script:
./install_and_run.sh


#### Docker Setup
1. Make the script executable:
chmod +x docker_install_and_run.sh

2. Run the script:
./docker_install_and_run.sh


### Manual Setup (Alternative Method)

#### Running the Bot without Docker
1. Navigate to the project directory.
2. Run the bot using .NET CLI:
dotnet run


#### Docker Setup
1. Create a `Dockerfile` in the root directory of your project with the following content:
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:latest
WORKDIR /app
COPY . ./ 
CMD ["dotnet", "YourBot.dll"]
Replace YourBot.dll with the name of your bot's assembly.

1. Build your Docker image:
docker build -t hartsybot .
2. Run the Docker container:
docker run -d --name hartsybot hartsybot

Docker Compose (Optional)
For Docker Compose, create a docker-compose.yml file:
version: '3.8'
services:
  hartsybot:
    build: .
    environment:
      - BOT_TOKEN=your_bot_token_here

Run using:
docker-compose up

Bot Commands
/setup_rules: Sets up rules for the server.
/ping: Responds with "Pong!".
Notes
Ensure your bot has appropriate permissions in your Discord server.
Replace your_bot_token_here with your actual bot token.

