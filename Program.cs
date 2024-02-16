using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using dotenv.net;
using dotenv.net.Utilities;
using System.IO;
using System.Reflection;

namespace HartsyBot
{
    class Program
    {
        private DiscordSocketClient? _client;
        private InteractionService? _interactions;

        public object? Context { get; private set; }

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // Try to get the bot token from environment variables
            var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

            // If token is not found in environment variables, load from .env file
            if (string.IsNullOrEmpty(token))
            {
                var envFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../.env"));
                Console.WriteLine("Attempting to load .env file from: " + envFilePath);

                if (File.Exists(envFilePath))
                {
                    var envOptions = new DotEnvOptions(envFilePaths: new[] { envFilePath });
                    DotEnv.Load(envOptions);
                    token = Environment.GetEnvironmentVariable("BOT_TOKEN");
                }
            }

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                LogLevel = LogSeverity.Debug
            };

            _client = new DiscordSocketClient(config);
            _interactions = new InteractionService(_client); // Initialize InteractionService

            _client.Log += Log;
            _interactions.Log += Log;
            _client.Ready += ReadyAsync;

            // Initialize and register event handlers
            var eventHandlers = new Core.EventHandlers(_client, _interactions);
            eventHandlers.RegisterHandlers();

            //var token = Environment.GetEnvironmentVariable("BOT_TOKEN"); // Get the bot token from environment variables
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Bot token is null or empty. Check your .env file.");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private async Task ReadyAsync()
        {
            try
            {
                // Things to be run when the bot is ready
                if (_client.Guilds.Any())
                {
                    // Register command modules with the InteractionService.
                    // Tells  to scan the whole assembly for classes that define slash commands.
                    await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), null);

                    // DEBUG: Delete all global commands
                    //await _client.Rest.DeleteAllGlobalCommandsAsync();

                    // Get the ID of the first guild the bot is a member of
                    // Then register the commands to that guild
                    var guildId = _client.Guilds.First().Id;
                    await _interactions.RegisterCommandsToGuildAsync(guildId, true);
                    //await _interactions.RegisterCommandsGloballyAsync(true);
                }
                else
                {
                    Console.WriteLine($"\nNo guilds found\n");
                }

                Console.WriteLine($"\nLogged in as {_client.CurrentUser.Username}\n" +
                    $"Registered {_interactions.SlashCommands.Count} slash commands\n" +
                    $"Bot is a member of {_client.Guilds.Count} guilds\n");
                await _client.SetGameAsync("/help", null, ActivityType.Listening);
            }
            catch (Exception e)
            {
                // Log the exception
                Console.WriteLine($"Exception: {e}");
                throw;
            }
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine($"{DateTime.Now} [{message.Severity}] {message.Source}: {message.Message}");
            if (message.Exception is not null) // Check if there is an exception
            {
                // Log the full exception, including the stack trace
                Console.WriteLine($"Exception: {message.Exception}");
            }
            return Task.CompletedTask;
        }
    }
}