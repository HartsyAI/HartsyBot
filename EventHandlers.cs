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
            // Get the default channel of the server
            var channel = user.Guild.DefaultChannel; 
            if (channel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");

                // Open the image file
                using (var stream = new FileStream(imagePath, FileMode.Open))
                {
                    // Message to be sent, welcoming the user who joined
                    string welcomeMessage = $"{user.Mention}, welcome to the Hartsy.AI Discord Server!";

                    // Send the image along with the welcome message
                    await channel.SendFileAsync(stream, "welcome.png", welcomeMessage);
                }
            }
        }
    }

}