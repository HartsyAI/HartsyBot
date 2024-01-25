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
            // Specify the name of the welcome channel
            string welcomeChannelName = "welcome";

            // Find the welcome channel by its name
            var welcomeChannel = user.Guild.TextChannels.FirstOrDefault(x => x.Name == welcomeChannelName);

            // Check if the welcome channel is found
            if (welcomeChannel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");

                // Open the image file
                using (var stream = new FileStream(imagePath, FileMode.Open))
                {
                    // Message to be sent, welcoming the user who joined
                    string welcomeMessage = $"{user.Mention}, welcome to the **Hartsy.AI** Discord Server! Check out the #rules channel.";

                    // Send the image along with the welcome message
                    await welcomeChannel.SendFileAsync(stream, "welcome.png", welcomeMessage);
                }
            }
        }
    }

}