using Discord;
using Discord.WebSocket;

namespace Hartsy.Core
{
    /// <summary>Handles Discord events and interactions for the bot, including user joins and role assignments based on subscription status.</summary>
    /// <remarks>Initializes a new instance of the EventHandlers class.</remarks>
    /// <param name="client">The Discord socket client instance.</param>
    /// <param name="supabaseClient">The client for interacting with the Supabase database.</param>
    public class EventHandlers(DiscordSocketClient client, SupabaseClient supabaseClient)
    {
        private readonly DiscordSocketClient _client = client;
        private readonly SupabaseClient _supabaseClient = supabaseClient;

        /// <summary>Registers the necessary Discord event handlers.</summary>
        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
        }

        /// <summary>Handles the event when a user joins the guild, assigning roles and sending welcome messages.</summary>
        /// <param name="user">The user who joined the guild.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            string[] channelNames = ["welcome", "rules", "generate", "info"];
            Dictionary<string, SocketTextChannel> channels = [];

            foreach (var name in channelNames)
            {
                var channel = user.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (channel != null)
                {
                    channels[name] = channel;
                }
            }
            channels.TryGetValue("welcome", out var welcomeChannel);
            channels.TryGetValue("rules", out var rulesChannel);
            channels.TryGetValue("generate", out var generateChannel);
            channels.TryGetValue("info", out var infoChannel);

            // Check if the user has linked their Discord account
            bool isLinked = await _supabaseClient.IsDiscordLinked(user.Id.ToString());

            if (welcomeChannel != null && rulesChannel != null && generateChannel != null)
            {
                // Path to the image
                var imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");
                // Open the image file
                using var stream = new FileStream(imagePath, FileMode.Open);

                var welcomeEmbed = new EmbedBuilder()
                    .WithTitle("Welcome to Hartsy.AI!")
                    .WithDescription($"{user.Mention}, we're thrilled to have you join the **Hartsy.AI** Discord Server!")
                    .AddField("Getting Started", $"Please check out the <#{rulesChannel.Id}> for our community guidelines and the " +
                    $"<#{infoChannel.Id}> for information on how to get the most out of our server.")
                    .AddField("Using the Bot", $"You can use our custom bot in the <#{generateChannel.Id}> channel to generate images. " +
                    $"Each image generation will consume one GPUT from your account.")
                    .AddField("About GPUTs", "GPUTs (GPU Time) are used as tokens for generating images. " +
                    "If you need more, you can purchase additional GPUTs on our website. You can make a 1 time purchase or choose a subscription")
                    .WithFooter("Enjoy your stay and unleash your creativity with Hartsy.AI!")
                    .WithColor(Discord.Color.Blue)
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithCurrentTimestamp()
                    .Build();


                var button = new ComponentBuilder()
                    .WithButton("Link Discord Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();

                await welcomeChannel.SendFileAsync(stream, "welcome.png", embed: welcomeEmbed, components: button);
                if (!isLinked)
                {
                    // Send a direct message to the user
                    var notLinkedEmbed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account " +
                                         "and log into Hartsy.AI using your Discord credentials. If you have already done that and are still having issues" +
                                         " contact an admin. This may be a bug.")
                        .WithColor(Discord.Color.Blue)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();
                    try
                    {
                        await user.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    catch
                    {
                        // If DM fails, send the message in the welcome channel
                        await welcomeChannel.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    return;
                }
                await AssignRoleBasedOnSubscription(user);
            }
        }

        /// <summary>Assigns a role to the user based on their subscription status in the database.</summary>
        /// <param name="user">The user to assign the role to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task AssignRoleBasedOnSubscription(SocketGuildUser user)
        {
            var userStatus = await _supabaseClient.GetSubStatus(user.Id.ToString());
            if (userStatus != null && userStatus.TryGetValue("PlanName", out object? value))
            {
                string subStatus = value.ToString() ?? "Free";
                var subRole = user.Guild.Roles.FirstOrDefault(role => role.Name.Equals(subStatus, StringComparison.OrdinalIgnoreCase));
                await user.AddRoleAsync(subRole);
            }
        }
    }
}
