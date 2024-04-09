using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using dotenv.net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Hartsy.Core;

namespace HartsyBot
{
    class Program
    {
        private DiscordSocketClient? _client;
        private InteractionService? _interactions;
        private IServiceProvider? _serviceProvider;

        /// <summary>Main entry point for the bot application.</summary>
        static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

        /// <summary>Starts the main execution of the bot program asynchronously. Initializes the client, services, and event handlers.</summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
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
                    var envOptions = new DotEnvOptions(envFilePaths: [envFilePath]);
                    DotEnv.Load(envOptions);
                    token = Environment.GetEnvironmentVariable("BOT_TOKEN");
                }
            }

            _serviceProvider = ConfigureServices();
            _client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
            _interactions = _serviceProvider.GetRequiredService<InteractionService>();

            _client.InteractionCreated += async interaction =>
            {
                SocketInteractionContext ctx = new(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, _serviceProvider);
            };

            _client.Log += Log;
            _interactions.Log += Log;
            _client.Ready += () => ReadyAsync();

            // Initialize and register event handlers
            SupabaseClient supabaseClient = _serviceProvider.GetRequiredService<SupabaseClient>();
            EventHandlers eventHandlers = new(_client, supabaseClient);
            eventHandlers.RegisterHandlers();

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

        /// <summary>Configures and provides the services used by the bot, including Discord client, interaction service, and command handling.</summary>
        /// <returns>A ServiceProvider containing the configured services.</returns>
        private static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.All,
                    LogLevel = LogSeverity.Debug
                }))
                .AddSingleton<InteractionService>()
                .AddSingleton<InteractionHandlers>()
                .AddSingleton<TemplateAutocompleteHandler>()
                .AddSingleton<SupabaseClient>()
                .AddSingleton<StableSwarmAPI>()
                .AddSingleton<Showcase>()
                .AddSingleton<Commands>()
                .BuildServiceProvider();
        }

        /// <summary>Executes tasks when the bot client is ready, such as command registration and initialization. 
        /// It registers commands to guilds and sets the bot status.</summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task ReadyAsync()
        {
            try
            {
                // Things to be run when the bot is ready
                if (_client!.Guilds.Count != 0)
                {
                    // Register command modules with the InteractionService.
                    // Tells  to scan the whole assembly for classes that define slash commands.
                    await _interactions!.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

                    foreach (var guild in _client.Guilds)
                    {
                        await _interactions.RegisterCommandsToGuildAsync(guild.Id, true);
                    }
                }
                else
                {
                    Console.WriteLine($"\nNo guilds found\n");
                }

                Console.WriteLine($"\nLogged in as {_client.CurrentUser.Username}\n" +
                    $"Registered {_interactions!.SlashCommands.Count} slash commands\n" +
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

        /// <summary>Handles logging of messages and exceptions from the Discord client and interaction services.</summary>
        /// <param name="message">The message to log.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
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