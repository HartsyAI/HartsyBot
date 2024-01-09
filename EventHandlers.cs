using System.Threading.Tasks;
using Discord.WebSocket;

namespace HartsyBot
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;

        public EventHandlers(DiscordSocketClient client)
        {
            _client = client;
        }

        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
            _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;
        }

        private async Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
        {
            match (command.Data.Name)
            {
                case "ping":
                    await command.RespondAsync("Pong!");
                    break;

                case "setup_rules":
                    // call the code from Commands.cs public async Task SetupRulesCommand()
                    
                    
                    break;
            }
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            var channel = user.Guild.DefaultChannel; // This gets the default channel of the guild (server)
            if (channel != null)
            {
                await channel.SendMessageAsync($"Welcome to the server, {user.Mention}!");
            }
        }
    }
}
