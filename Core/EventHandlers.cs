using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Supabase;
using Supabase.Interfaces;

namespace HartsyBot.Core
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly SupabaseClient _supabaseClient;

        public EventHandlers(DiscordSocketClient client, InteractionService interactions, SupabaseClient supabaseClient)
        {
            _client = client;
            _interactions = interactions;
            _supabaseClient = supabaseClient;
        }

        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            // Specify the name of the welcome channel
            string welcomeChannelName = "welcome";

            // Find the welcome channel by its name
            var welcomeChannel = user.Guild.TextChannels.FirstOrDefault(x => x.Name == welcomeChannelName);

            // Check if the user has linked their Discord account
            bool isLinked = await _supabaseClient.IsDiscordLinked(user.Id.ToString());

            // Check if the welcome channel is found
            if (welcomeChannel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");

                // Open the image file
                using var stream = new FileStream(imagePath, FileMode.Open);
                // Message to be sent, welcoming the user who joined
                string welcomeMessage = $"{user.Mention}, welcome to the **Hartsy.AI** Discord Server! Check out the #rules channel.";

                // Send the image along with the welcome message
                await welcomeChannel.SendFileAsync(stream, "welcome.png", welcomeMessage);
            }
            if (!isLinked)
            {
                // Send a direct message to the user
                await user.SendMessageAsync(user.Mention + ", you have not linked your Discord account with your Hartsy.AI account. Please link your account to access the server.");
            }
        }
    }

}