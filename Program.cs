using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using dotenv.net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System;
using Hartsy.Core;
using HartsyBot.Core;

namespace HartsyBot
{
    class Program
    {
        private DiscordSocketClient? _client;
        private InteractionService? _interactions;
        private IServiceProvider? _serviceProvider;
        private IServiceCollection? _services;

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

            _serviceProvider = ConfigureServices();
            _client = _serviceProvider.GetRequiredService<DiscordSocketClient>();
            _interactions = _serviceProvider.GetRequiredService<InteractionService>();

            _client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, _serviceProvider);
            };

            _client.Log += Log;
            _interactions.Log += Log;
            _client.Ready += () => ReadyAsync(_serviceProvider);

            // Initialize and register event handlers
            var supabaseClient = _serviceProvider.GetRequiredService<SupabaseClient>();
            var eventHandlers = new Core.EventHandlers(_client, _interactions, supabaseClient);
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

        private ServiceProvider ConfigureServices()
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
                .AddSingleton<RunpodAPI>()
                .AddSingleton<StableSwarmAPI>()
                .AddSingleton<Showcase>()
                .AddSingleton<Commands>()
                .BuildServiceProvider();
        }

        private async Task ReadyAsync(IServiceProvider services)
        {
            try
            {
                // Things to be run when the bot is ready
                if (_client.Guilds.Any())
                {
                    // Register command modules with the InteractionService.
                    // Tells  to scan the whole assembly for classes that define slash commands.
                    await _interactions.RegisterCommandsGloballyAsync(true);
                    await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

                    // DEBUG: Delete all global commands
                    //await _client.Rest.DeleteAllGlobalCommandsAsync();

                    // Get the ID of the first guild the bot is a member of
                    // Then register the commands to that guild
                    //var guildId = _client.Guilds.First().Id;
                    //await _interactions.RegisterCommandsToGuildAsync(guildId, true);
                    //await _interactions.RegisterCommandsGloballyAsync(true);
                    //await _client.Rest.DeleteAllGlobalCommandsAsync();


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