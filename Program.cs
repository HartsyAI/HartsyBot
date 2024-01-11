using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using dotenv.net;
using dotenv.net.Utilities;
using System.IO;

namespace HartsyBot
{
    class Program
    {
        private DiscordSocketClient? _client;
        private InteractionService? _interactions;

        public object Context { get; private set; }

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var envFilePath = Path.GetFullPath("../../../.env");
            Console.WriteLine("Attempting to load .env file from: " + envFilePath);

            var envOptions = new DotEnvOptions(envFilePaths: new[] { envFilePath });
            DotEnv.Load(envOptions);

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All
            };

            _client = new DiscordSocketClient(config);
            _interactions = new InteractionService(_client); // Initialize InteractionService

            // Register command modules
            await _interactions.AddModulesAsync(typeof(Program).Assembly, null);


            _client.Log += LogAsync;

            // Initialize and register event handlers
            var eventHandlers = new EventHandlers(_client, _interactions);
            eventHandlers.RegisterHandlers();

            var token = Environment.GetEnvironmentVariable("BOT_TOKEN"); // Get the bot token from environment variables
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Bot token is null or empty. Check your .env file.");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += async () =>
            {
                var guild = _client.Guilds.FirstOrDefault(); // Get the first guild the bot is in
                if (guild != null)
                {
                    ulong guildId = guild.Id;
                    if (guild != null)
                    {
                        //await _interactions.AddCommandsToGuildAsync(guildId, true);
                        await _interactions.RegisterCommandsGloballyAsync(true);
                    }
                    else
                    {
                        await _interactions.RegisterCommandsGloballyAsync(true);
                    }
                }
            };
            // Wait for a short period to ensure commands are registered
            await Task.Delay(1000); // Delay for 1 second

        // Block this task until the program is closed.
        await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}