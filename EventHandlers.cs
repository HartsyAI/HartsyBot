using System.Threading.Tasks;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.VisualBasic;

namespace HartsyBot
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;

        public EventHandlers(DiscordSocketClient client, InteractionService interactions)
        {
            _client = client;
            _interactions = interactions;
        }

        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
            _client.InteractionCreated += async interaction =>
            {
                var ctx = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(ctx, null);
            };
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            var channel = user.Guild.DefaultChannel; // Default channel of the server
            if (channel != null)
            {
                await channel.SendMessageAsync($"Welcome to the server, {user.Mention}!");
            }
        }
    }
}